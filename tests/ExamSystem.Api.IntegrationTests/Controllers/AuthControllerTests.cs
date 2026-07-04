using System.Net;
using System.Net.Http.Json;
using ExamSystem.Application.Features.Auth.Login;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

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
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand("admin", "totally-wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
