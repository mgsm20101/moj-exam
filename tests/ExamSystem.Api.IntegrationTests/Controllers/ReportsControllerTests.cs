using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class ReportsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly TestWebApplicationFactory _factory;
    public ReportsControllerTests(TestWebApplicationFactory factory) => _factory = factory;

    private static object Identity => new { fullName = "احمد محمد علي حسن", nationalId = "29912310123404", mobileNumber = "01012345678" };

    // Admin creates a published, open exam with 2 correct-answerable MCQ questions (total 4 marks, pass mark 60%).
    private static async Task<Guid> CreatePublishedExamAsync(HttpClient admin)
    {
        var topicResp = await admin.PostAsJsonAsync("/api/admin/topics", new { name = $"T{Guid.NewGuid():N}", displayOrder = 1 });
        topicResp.EnsureSuccessStatusCode();
        var topicId = (await topicResp.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        for (var i = 0; i < 2; i++)
        {
            (await admin.PostAsJsonAsync("/api/admin/questions", new
            {
                topicId, type = "Mcq", difficulty = "Medium", text = $"Q{i}",
                options = new[] { new { text = "wrong", isCorrect = false }, new { text = "right", isCorrect = true } }
            })).EnsureSuccessStatusCode();
        }
        var examResp = await admin.PostAsJsonAsync("/api/admin/exams", new
        {
            name = $"Exam {Guid.NewGuid():N}", description = (string?)null,
            startAtUtc = DateTime.UtcNow.AddMinutes(-5), endAtUtc = DateTime.UtcNow.AddHours(2),
            durationMinutes = 60, mcqPoints = 2m, trueFalsePoints = 1m, fillBlankPoints = 5m,
            passMarkPercentage = 60m, maxAttempts = 1, maxConcurrentAttempts = 20, graceWindowMinutes = 3, shuffleAnswers = true,
            showResultImmediately = true, allowBackNavigation = true,
            topicSelections = new[] { new { topicId, displayOrder = 1, difficulty = "Medium", type = "Mcq", count = 2 } }
        });
        examResp.EnsureSuccessStatusCode();
        var examId = (await examResp.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        (await admin.PostAsync($"/api/admin/exams/{examId}/publish", null)).EnsureSuccessStatusCode();
        return examId;
    }

    // A candidate takes the exam, answering every question correctly, then submits -> a completed passing attempt.
    private async Task TakeAndPassAsync(Guid examId)
    {
        var anon = _factory.CreateClient();
        var start = await (await anon.PostAsJsonAsync($"/api/exam/{examId}/start", Identity))
            .Content.ReadFromJsonAsync<StartResponse>();
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", start!.AttemptToken);

        var state = await (await anon.GetAsync($"/api/exam/{examId}/attempt/state")).Content.ReadFromJsonAsync<StateResponse>();
        foreach (var q in state!.Questions)
        {
            var right = q.Options.First(o => o.Text == "right").Id;
            (await anon.PostAsJsonAsync($"/api/exam/{examId}/attempt/answer",
                new { attemptQuestionId = q.AttemptQuestionId, selectedOptionId = right, answerText = (string?)null, isFlagged = false }))
                .EnsureSuccessStatusCode();
        }
        (await anon.PostAsync($"/api/exam/{examId}/attempt/submit", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Results_AfterOnePassingAttempt_ReturnsReportWithOnePasser()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        await TakeAndPassAsync(examId);

        var resp = await admin.GetAsync($"/api/admin/reports/exams/{examId}/results");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var report = await resp.Content.ReadFromJsonAsync<ReportResponse>();
        Assert.Equal(4m, report!.TotalPoints);
        Assert.Equal(1, report.Summary.TotalCandidates);
        Assert.Equal(1, report.Summary.PassedCount);
        Assert.Equal(0, report.Summary.FailedCount);
        var row = Assert.Single(report.Rows);
        Assert.Equal("29912310123404", row.NationalId);
        Assert.True(row.Passed);
    }

    [Fact]
    public async Task Export_ReturnsXlsxFile()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        await TakeAndPassAsync(examId);

        var resp = await admin.GetAsync($"/api/admin/reports/exams/{examId}/results/export");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(XlsxContentType, resp.Content.Headers.ContentType!.MediaType);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        // .xlsx is a ZIP archive -> starts with "PK".
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public async Task Results_Filter_ParsesCaseInsensitively_AndFallsBackToAllOnGarbage()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        await TakeAndPassAsync(examId); // one passer, zero failers

        var passed = await (await admin.GetAsync($"/api/admin/reports/exams/{examId}/results?filter=PASSED"))
            .Content.ReadFromJsonAsync<ReportResponse>();
        Assert.Single(passed!.Rows);                       // case-insensitive parse -> Passed
        Assert.Equal(1, passed.Summary.TotalCandidates);

        var failed = await (await admin.GetAsync($"/api/admin/reports/exams/{examId}/results?filter=failed"))
            .Content.ReadFromJsonAsync<ReportResponse>();
        Assert.Empty(failed!.Rows);                        // no failers
        Assert.Equal(1, failed.Summary.TotalCandidates);   // summary unchanged by the filter

        var garbage = await admin.GetAsync($"/api/admin/reports/exams/{examId}/results?filter=not-a-filter");
        Assert.Equal(HttpStatusCode.OK, garbage.StatusCode); // unparseable -> falls back to All (no 400/500)
        var garbageBody = await garbage.Content.ReadFromJsonAsync<ReportResponse>();
        Assert.Single(garbageBody!.Rows);
    }

    [Fact]
    public async Task Export_WorkbookContainsTheCandidateRowAndPassLabel()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        await TakeAndPassAsync(examId);

        var bytes = await (await admin.GetAsync($"/api/admin/reports/exams/{examId}/results/export")).Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var results = workbook.Worksheet("النتائج");
        var cells = results.RangeUsed()!.Cells().Select(c => c.GetString()).ToList();
        Assert.Contains("29912310123404", cells); // the candidate's national ID
        Assert.Contains("ناجح", cells);            // the pass label
        Assert.NotNull(workbook.Worksheet("الملخص")); // summary sheet exists
    }

    [Fact]
    public async Task Export_WithFailedFilter_WritesHeaderOnly_WhenThereAreNoFailers()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        await TakeAndPassAsync(examId); // one passer, zero failers

        var bytes = await (await admin.GetAsync($"/api/admin/reports/exams/{examId}/results/export?filter=failed")).Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var results = workbook.Worksheet("النتائج");
        // Only the header row is present -> the failed filter reached the export.
        Assert.Equal(1, results.RangeUsed()!.RowCount());
    }

    [Fact]
    public async Task Results_WithoutAuth_ReturnsUnauthorized()
    {
        var anon = _factory.CreateClient();

        var resp = await anon.GetAsync($"/api/admin/reports/exams/{Guid.NewGuid()}/results");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Results_UnknownExam_ReturnsNotFound()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();

        var resp = await admin.GetAsync($"/api/admin/reports/exams/{Guid.NewGuid()}/results");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    private record IdResponse(Guid Id);
    private record StartResponse(Guid AttemptId, string AttemptToken, DateTime ExpiresAtUtc);
    private record OptionResponse(Guid Id, string Text);
    private record QuestionResponse(Guid AttemptQuestionId, int DisplayOrder, string Type, string Text, string? ImageUrl,
        List<OptionResponse> Options, Guid? SelectedOptionId, string? AnswerText, bool IsFlagged);
    private record StateResponse(string Status, int RemainingSeconds, bool ShowResultImmediately, List<QuestionResponse> Questions);
    private record SummaryResponse(int TotalCandidates, int PassedCount, int FailedCount, decimal PassRatePercentage);
    private record RowResponse(string FullName, string NationalId, string MobileNumber, decimal Score, decimal TotalPoints,
        decimal ScorePercentage, bool Passed, DateTime? SubmittedAtUtc, int GovernorateCode, int TabSwitchCount);
    private record ReportResponse(Guid ExamId, string ExamName, decimal TotalPoints, decimal PassMarkPercentage,
        decimal PassMarkPoints, string Filter, SummaryResponse Summary, List<RowResponse> Rows);
}
