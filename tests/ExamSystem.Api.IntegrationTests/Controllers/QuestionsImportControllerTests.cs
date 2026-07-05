using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class QuestionsImportControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public QuestionsImportControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Import_RealPreparedWorkbook_ImportsAllRowsWithNoFailures()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();

        var filePath = Path.Combine(AppContext.BaseDirectory, "TestData", "questions_ready_for_import.xlsx");
        await using var fileStream = File.OpenRead(filePath);

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(streamContent, "file", "questions_ready_for_import.xlsx");

        var response = await client.PostAsync("/api/admin/questions/import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<BulkImportReportDto>();
        Assert.Equal(352, report!.TotalRows);
        Assert.Equal(352, report.SuccessCount);
        Assert.Empty(report.Errors);

        var listResponse = await client.GetAsync("/api/admin/questions");
        var questions = await listResponse.Content.ReadFromJsonAsync<List<QuestionListItemDto>>();
        Assert.Equal(352, questions!.Count);
    }

    private record BulkImportRowErrorDto(string Sheet, int RowNumber, string Message);
    private record BulkImportReportDto(int TotalRows, int SuccessCount, int FailureCount, List<BulkImportRowErrorDto> Errors);
    private record QuestionListItemDto(Guid Id, string Text);
}
