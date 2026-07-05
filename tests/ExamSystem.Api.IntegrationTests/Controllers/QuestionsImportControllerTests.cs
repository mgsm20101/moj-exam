using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
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
    public async Task Import_MisnamedMcqSheet_FailsWithClearErrorInsteadOfSilentlyDroppingRows()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();

        using var workbook = new XLWorkbook();
        var mcqSheet = workbook.Worksheets.Add("Questions"); // misnamed — should be "MCQ"
        WriteMcqHeader(mcqSheet);
        WriteMcqRow(mcqSheet, 2, "Topic1", "Easy", "2+2=?", "3", "4", "5", "6", "B");
        var fillBlankSheet = workbook.Worksheets.Add("FillBlank");
        WriteFillBlankHeader(fillBlankSheet);

        var response = await PostWorkbookAsync(client, workbook);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorsDto>();
        Assert.Contains(body!.Errors, e => e.Contains("MCQ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_ReorderedMcqColumns_FailsWithClearErrorInsteadOfMisreadingData()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();

        using var workbook = new XLWorkbook();
        var mcqSheet = workbook.Worksheets.Add("MCQ");
        // CorrectOption and OptionA swapped relative to the expected column order.
        mcqSheet.Cell(1, 1).Value = "Topic";
        mcqSheet.Cell(1, 2).Value = "Type";
        mcqSheet.Cell(1, 3).Value = "Difficulty";
        mcqSheet.Cell(1, 4).Value = "QuestionText";
        mcqSheet.Cell(1, 5).Value = "CorrectOption";
        mcqSheet.Cell(1, 6).Value = "OptionB";
        mcqSheet.Cell(1, 7).Value = "OptionC";
        mcqSheet.Cell(1, 8).Value = "OptionD";
        mcqSheet.Cell(1, 9).Value = "OptionA";
        mcqSheet.Cell(2, 1).Value = "Topic1";
        mcqSheet.Cell(2, 2).Value = "MCQ";
        mcqSheet.Cell(2, 3).Value = "Easy";
        mcqSheet.Cell(2, 4).Value = "2+2=?";
        mcqSheet.Cell(2, 5).Value = "B";
        mcqSheet.Cell(2, 6).Value = "4";
        mcqSheet.Cell(2, 7).Value = "5";
        mcqSheet.Cell(2, 8).Value = "6";
        mcqSheet.Cell(2, 9).Value = "3";
        var fillBlankSheet = workbook.Worksheets.Add("FillBlank");
        WriteFillBlankHeader(fillBlankSheet);

        var response = await PostWorkbookAsync(client, workbook);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorsDto>();
        Assert.Contains(body!.Errors, e => e.Contains("header row", StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteMcqHeader(IXLWorksheet sheet)
    {
        string[] headers = ["Topic", "Type", "Difficulty", "QuestionText", "OptionA", "OptionB", "OptionC", "OptionD", "CorrectOption"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }
    }

    private static void WriteMcqRow(IXLWorksheet sheet, int rowNumber, string topic, string difficulty,
        string questionText, string optionA, string optionB, string optionC, string optionD, string correctOption)
    {
        sheet.Cell(rowNumber, 1).Value = topic;
        sheet.Cell(rowNumber, 2).Value = "MCQ";
        sheet.Cell(rowNumber, 3).Value = difficulty;
        sheet.Cell(rowNumber, 4).Value = questionText;
        sheet.Cell(rowNumber, 5).Value = optionA;
        sheet.Cell(rowNumber, 6).Value = optionB;
        sheet.Cell(rowNumber, 7).Value = optionC;
        sheet.Cell(rowNumber, 8).Value = optionD;
        sheet.Cell(rowNumber, 9).Value = correctOption;
    }

    private static void WriteFillBlankHeader(IXLWorksheet sheet)
    {
        string[] headers = ["Topic", "Type", "Difficulty", "QuestionText", "CorrectAnswer"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }
    }

    private static async Task<HttpResponseMessage> PostWorkbookAsync(HttpClient client, XLWorkbook workbook)
    {
        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        memoryStream.Position = 0;

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(memoryStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(streamContent, "file", "workbook.xlsx");

        return await client.PostAsync("/api/admin/questions/import", content);
    }

    private record ErrorsDto(string[] Errors);

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
