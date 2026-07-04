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
}
