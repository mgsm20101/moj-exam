using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class CandidateAttemptControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public CandidateAttemptControllerTests(TestWebApplicationFactory factory) => _factory = factory;

    private static object Identity => new { fullName = "احمد محمد علي حسن", nationalId = "29912310123404", mobileNumber = "01012345678" };

    // Admin creates a published, open exam with 2 MCQ questions and a selection of 2.
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

    private async Task<(HttpClient client, Guid examId)> StartAttemptAsync()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        var anon = _factory.CreateClient();
        var startResp = await anon.PostAsJsonAsync($"/api/exam/{examId}/start", Identity);
        startResp.EnsureSuccessStatusCode();
        var start = await startResp.Content.ReadFromJsonAsync<StartResponse>();
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", start!.AttemptToken);
        return (anon, examId);
    }

    [Fact]
    public async Task State_ReturnsSanitizedQuestions_NoCorrectnessLeak()
    {
        var (client, examId) = await StartAttemptAsync();

        var resp = await client.GetAsync($"/api/exam/{examId}/attempt/state");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("isCorrect", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctAnswer", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Answer_ThenSubmit_GradesCorrectly()
    {
        var (client, examId) = await StartAttemptAsync();
        var state = await (await client.GetAsync($"/api/exam/{examId}/attempt/state")).Content.ReadFromJsonAsync<StateResponse>();

        foreach (var q in state!.Questions)
        {
            var right = q.Options.First(o => o.Text == "right").Id;
            var save = await client.PostAsJsonAsync($"/api/exam/{examId}/attempt/answer",
                new { attemptQuestionId = q.AttemptQuestionId, selectedOptionId = right, answerText = (string?)null, isFlagged = false });
            Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        }

        var submit = await client.PostAsync($"/api/exam/{examId}/attempt/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var result = await submit.Content.ReadFromJsonAsync<ResultResponse>();
        Assert.True(result!.Shown);
        Assert.Equal(4m, result.Score);   // 2 questions x 2 points
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task State_WithoutToken_IsUnauthorized()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        var anon = _factory.CreateClient(); // no attempt token

        var resp = await anon.GetAsync($"/api/exam/{examId}/attempt/state");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private record IdResponse(Guid Id);
    private record StartResponse(Guid AttemptId, string AttemptToken, DateTime ExpiresAtUtc);
    private record OptionResponse(Guid Id, string Text);
    private record QuestionResponse(Guid AttemptQuestionId, int DisplayOrder, string Type, string Text, string? ImageUrl,
        List<OptionResponse> Options, Guid? SelectedOptionId, string? AnswerText, bool IsFlagged);
    private record StateResponse(string Status, int RemainingSeconds, bool ShowResultImmediately, List<QuestionResponse> Questions);
    private record ResultResponse(bool Shown, decimal Score, decimal TotalPoints, decimal PassMarkPercentage, bool Passed);
}
