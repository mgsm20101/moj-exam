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

    [Fact]
    public async Task UploadImage_ThenGetReturnedUrl_ServesTheUploadedFile()
    {
        // Regression test for a bug where uploaded images were saved to disk successfully
        // (201/200 OK from the upload endpoint) but the static file middleware returned 404
        // when fetching the returned URL, because wwwroot didn't exist on disk when the host
        // was built and ASP.NET Core cached a NullFileProvider for static files as a result.
        var client = await _factory.CreateAuthenticatedAdminClientAsync();

        using var content = new MultipartFormDataContent();
        var imageBytes = CreateMinimalPngBytes();
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test-image.png");

        var uploadResponse = await client.PostAsync("/api/admin/questions/image", content);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var body = await uploadResponse.Content.ReadFromJsonAsync<UploadedImageDto>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Url));

        var getResponse = await client.GetAsync(body.Url);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    private static byte[] CreateMinimalPngBytes()
    {
        // A valid 1x1 transparent PNG.
        const string base64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
        return Convert.FromBase64String(base64);
    }

    private record CreatedIdDto(Guid Id);
    private record QuestionListItemDto(Guid Id, string Text);
    private record UploadedImageDto(string Url);
}
