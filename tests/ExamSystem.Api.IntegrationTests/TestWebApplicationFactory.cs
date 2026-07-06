using ExamSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Json;

namespace ExamSystem.Api.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SeedAdminPassword = "Test-P@ssw0rd!1";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "integration-test-signing-key-please-ignore-32chars",
                ["Jwt:Issuer"] = "ExamSystem.Tests",
                ["Jwt:Audience"] = "ExamSystem.Tests.Clients",
                ["AttemptToken:Key"] = "integration-test-attempt-token-key-please-ignore-32chars",
                ["AttemptToken:Issuer"] = "ExamSystem.Tests",
                ["AttemptToken:Audience"] = "ExamSystem.Tests.Candidates",
                ["SeedAdmin:UserName"] = "admin",
                ["SeedAdmin:Password"] = SeedAdminPassword,
                ["SeedAdmin:Email"] = "admin@examsystem.local"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    public async Task<HttpClient> CreateAuthenticatedAdminClientAsync()
    {
        var client = CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "admin",
            password = SeedAdminPassword
        });
        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private record LoginResponseDto(string Token, string UserName, IReadOnlyList<string> Roles);
}
