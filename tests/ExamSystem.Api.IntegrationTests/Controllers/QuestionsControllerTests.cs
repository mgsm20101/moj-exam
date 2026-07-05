using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class QuestionsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public QuestionsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> CreateTopicAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/admin/topics", new { name, displayOrder = 1 });
        var body = await response.Content.ReadFromJsonAsync<CreatedIdDto>();
        return body!.Id;
    }

    [Fact]
    public async Task CreateFillBlankQuestion_WithInvalidAnswerFormat_ReturnsBadRequest()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Word Skills");

        var response = await client.PostAsJsonAsync("/api/admin/questions", new
        {
            topicId,
            type = "FillBlank",
            difficulty = "Medium",
            text = "Fill ___",
            correctAnswerText = "Data Base"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateMcqQuestion_ThenList_ReturnsIt()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Windows Basics");

        var createResponse = await client.PostAsJsonAsync("/api/admin/questions", new
        {
            topicId,
            type = "Mcq",
            difficulty = "Hard",
            text = "What does CPU stand for?",
            options = new[]
            {
                new { text = "Central Processing Unit", isCorrect = true },
                new { text = "Central Printer Unit", isCorrect = false }
            }
        });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var listResponse = await client.GetAsync($"/api/admin/questions?topicId={topicId}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var questions = await listResponse.Content.ReadFromJsonAsync<List<QuestionListItemDto>>();
        Assert.Single(questions!, q => q.Text == "What does CPU stand for?");
    }

    private record CreatedIdDto(Guid Id);
    private record QuestionListItemDto(Guid Id, string Text);
}
