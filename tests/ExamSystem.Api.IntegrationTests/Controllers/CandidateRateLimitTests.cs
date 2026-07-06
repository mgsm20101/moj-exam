using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class CandidateRateLimitTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public CandidateRateLimitTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_ExceedingTheLimit_Returns429()
    {
        // Override the (otherwise generous) limit to a small value just for this test's host.
        var strictFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Candidate:PermitLimit"] = "3",
                    ["RateLimiting:Candidate:WindowSeconds"] = "60"
                })));

        var client = strictFactory.CreateClient();
        var body = new { fullName = "احمد محمد علي حسن", nationalId = "29912310123404", mobileNumber = "01012345678" };
        var examId = Guid.NewGuid(); // unknown exam is fine; the limiter runs before the handler

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var resp = await client.PostAsJsonAsync($"/api/exam/{examId}/register", body);
            statuses.Add(resp.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
