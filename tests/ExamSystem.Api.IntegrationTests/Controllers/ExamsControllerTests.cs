using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class ExamsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ExamsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<Guid> CreateTopicAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/admin/topics", new { name, displayOrder = 1 });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Id;
    }

    private static async Task CreateMcqQuestionAsync(HttpClient client, Guid topicId, string difficulty)
    {
        var response = await client.PostAsJsonAsync("/api/admin/questions", new
        {
            topicId,
            type = "Mcq",
            difficulty,
            text = "Pick one",
            options = new[] { new { text = "A", isCorrect = false }, new { text = "B", isCorrect = true } }
        });
        response.EnsureSuccessStatusCode();
    }

    private static object BuildExamPayload(Guid topicId, int count) => new
    {
        name = $"Exam {Guid.NewGuid():N}",
        description = (string?)null,
        startAtUtc = DateTime.UtcNow.AddDays(1),
        endAtUtc = DateTime.UtcNow.AddDays(8),
        durationMinutes = 60,
        mcqPoints = 2m,
        trueFalsePoints = 1m,
        fillBlankPoints = 5m,
        passMarkPercentage = 60m,
        maxAttempts = 1,
        maxConcurrentAttempts = 20,
        graceWindowMinutes = 3,
        shuffleAnswers = true,
        showResultImmediately = true,
        allowBackNavigation = true,
        topicSelections = new[] { new { topicId, displayOrder = 1, difficulty = "Medium", type = "Mcq", count } }
    };

    [Fact]
    public async Task CreateThenGet_ReturnsTheCreatedExamAsDraft()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Excel Skills - Exams Create");

        var createResponse = await client.PostAsJsonAsync("/api/admin/exams", BuildExamPayload(topicId, 1));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        var getResponse = await client.GetAsync($"/api/admin/exams/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var detail = await getResponse.Content.ReadFromJsonAsync<ExamDetailResponse>();
        Assert.Equal("Draft", detail!.Status);
        Assert.Single(detail.TopicSelections);
    }

    [Fact]
    public async Task Publish_WithInsufficientQuestionBank_ReturnsBadRequest()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Excel Skills - Publish Fail");

        var createResponse = await client.PostAsJsonAsync("/api/admin/exams", BuildExamPayload(topicId, count: 5));
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        var publishResponse = await client.PostAsync($"/api/admin/exams/{created!.Id}/publish", null);

        Assert.Equal(HttpStatusCode.BadRequest, publishResponse.StatusCode);
    }

    [Fact]
    public async Task Publish_WithSufficientQuestionBank_SucceedsThenCloseThenArchive()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Excel Skills - Full Lifecycle");
        await CreateMcqQuestionAsync(client, topicId, "Medium");

        var createResponse = await client.PostAsJsonAsync("/api/admin/exams", BuildExamPayload(topicId, count: 1));
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        var publishResponse = await client.PostAsync($"/api/admin/exams/{created!.Id}/publish", null);
        Assert.Equal(HttpStatusCode.NoContent, publishResponse.StatusCode);

        var closeResponse = await client.PostAsync($"/api/admin/exams/{created.Id}/close", null);
        Assert.Equal(HttpStatusCode.NoContent, closeResponse.StatusCode);

        var archiveResponse = await client.PostAsync($"/api/admin/exams/{created.Id}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/admin/exams/{created.Id}");
        var detail = await getResponse.Content.ReadFromJsonAsync<ExamDetailResponse>();
        Assert.Equal("Archived", detail!.Status);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/exams", BuildExamPayload(Guid.NewGuid(), 1));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record IdResponse(Guid Id);
    private record ExamDetailResponse(Guid Id, string Status, List<object> TopicSelections);
}
