using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class TopicsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public TopicsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateThenGet_ReturnsTheCreatedTopic()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/admin/topics", new { name = "Excel Skills", displayOrder = 1 });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/admin/topics");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var topics = await getResponse.Content.ReadFromJsonAsync<List<TopicDto>>();
        Assert.Contains(topics!, t => t.Name == "Excel Skills");
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/topics", new { name = "No Auth", displayOrder = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record TopicDto(Guid Id, string Name, int DisplayOrder, bool IsActive, int QuestionCount);
}
