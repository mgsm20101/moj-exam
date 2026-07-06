using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class CandidateExamControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CandidateExamControllerTests(TestWebApplicationFactory factory) => _factory = factory;

    // 1999-12-31, Cairo(01), Female
    private const string Nid = "29912310123404";
    private static object Identity => new { fullName = "احمد محمد علي حسن", nationalId = Nid, mobileNumber = "01012345678" };

    // Builds a published, currently-open exam with a bank of 3 MCQs and a selection of 2.
    private static async Task<Guid> CreatePublishedExamAsync(HttpClient admin)
    {
        var topicResp = await admin.PostAsJsonAsync("/api/admin/topics", new { name = $"T{Guid.NewGuid():N}", displayOrder = 1 });
        topicResp.EnsureSuccessStatusCode();
        var topicId = (await topicResp.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        for (var i = 0; i < 3; i++)
        {
            var q = await admin.PostAsJsonAsync("/api/admin/questions", new
            {
                topicId, type = "Mcq", difficulty = "Medium", text = $"Q{i}",
                options = new[] { new { text = "A", isCorrect = false }, new { text = "B", isCorrect = true } }
            });
            q.EnsureSuccessStatusCode();
        }

        var examResp = await admin.PostAsJsonAsync("/api/admin/exams", new
        {
            name = $"Exam {Guid.NewGuid():N}", description = (string?)null,
            startAtUtc = DateTime.UtcNow.AddMinutes(-5), endAtUtc = DateTime.UtcNow.AddHours(2),
            durationMinutes = 60, mcqPoints = 2m, trueFalsePoints = 1m, fillBlankPoints = 5m,
            passMarkPercentage = 60m, maxAttempts = 1, shuffleAnswers = true,
            showResultImmediately = true, allowBackNavigation = true,
            topicSelections = new[] { new { topicId, displayOrder = 1, difficulty = "Medium", type = "Mcq", count = 2 } }
        });
        examResp.EnsureSuccessStatusCode();
        var examId = (await examResp.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var publishResp = await admin.PostAsync($"/api/admin/exams/{examId}/publish", null);
        publishResp.EnsureSuccessStatusCode();
        return examId;
    }

    [Fact]
    public async Task Landing_PublishedExam_ReturnsOpen()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);

        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync($"/api/exam/{examId}/landing");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LandingResponse>();
        Assert.True(body!.IsOpen);
        Assert.Equal(2, body.TotalQuestionCount);
    }

    [Fact]
    public async Task Register_NewCandidate_ReturnsCanStart()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);

        var anon = _factory.CreateClient();
        var resp = await anon.PostAsJsonAsync($"/api/exam/{examId}/register", Identity);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.Equal("CanStart", body!.Status);
    }

    [Fact]
    public async Task Start_ThenStartAgain_IsIdempotentAndReturnsToken()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        var anon = _factory.CreateClient();

        var first = await anon.PostAsJsonAsync($"/api/exam/{examId}/start", Identity);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<StartResponse>();
        Assert.False(string.IsNullOrWhiteSpace(firstBody!.AttemptToken));

        var second = await anon.PostAsJsonAsync($"/api/exam/{examId}/start", Identity);
        var secondBody = await second.Content.ReadFromJsonAsync<StartResponse>();
        Assert.Equal(firstBody.AttemptId, secondBody!.AttemptId);
    }

    [Fact]
    public async Task Register_InvalidNationalId_ReturnsBadRequest()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        var anon = _factory.CreateClient();

        var resp = await anon.PostAsJsonAsync($"/api/exam/{examId}/register",
            new { fullName = "احمد محمد علي حسن", nationalId = "123", mobileNumber = "01012345678" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private record IdResponse(Guid Id);
    private record LandingResponse(Guid ExamId, string Name, string? Description, bool IsOpen, int DurationMinutes, int TotalQuestionCount);
    private record RegisterResponse(string Status, Guid CandidateId);
    private record StartResponse(Guid AttemptId, string AttemptToken, DateTime ExpiresAtUtc);
}
