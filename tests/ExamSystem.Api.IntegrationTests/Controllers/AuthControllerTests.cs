using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ExamSystem.Application.Features.Auth.Login;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

// Both tests share one TestWebApplicationFactory instance (and one seeded admin row) via IClassFixture.
// Identity's lockout policy allows 5 failed attempts before locking the account (see DependencyInjection.cs).
// This class currently has only 1 failing-password test, well within that budget. If you add more
// failure-path tests here, either reset AccessFailedCount between tests or use a non-admin/throwaway
// username for "bad credentials" scenarios, to avoid accidentally locking out the shared admin account.
public class AuthControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_ReturnsOkWithToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand("admin", TestWebApplicationFactory.SeedAdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Contains("Admin", body.Roles);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);
        Assert.Equal("ExamSystem.Tests", jwt.Issuer);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand("admin", "totally-wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokenPair()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand("admin", TestWebApplicationFactory.SeedAdminPassword));
        var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(loginBody!.RefreshToken));

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = loginBody.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = await refresh.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(refreshed!.Token));
        Assert.False(string.IsNullOrWhiteSpace(refreshed.RefreshToken));
        // Rotation: the new refresh token must differ from the one just consumed.
        Assert.NotEqual(loginBody.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithRotatedOutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand("admin", TestWebApplicationFactory.SeedAdminPassword));
        var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>();

        // First refresh consumes (and revokes) the original token.
        await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = loginBody!.RefreshToken });

        // Re-using the now-revoked original token must fail.
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = loginBody.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithBogusToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand("admin", TestWebApplicationFactory.SeedAdminPassword));
        var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>();

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = loginBody!.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // A revoked token can no longer be refreshed.
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = loginBody.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
