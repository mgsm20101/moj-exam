using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class CandidateQueueControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public CandidateQueueControllerTests(TestWebApplicationFactory factory) => _factory = factory;

    private static object A => new { fullName = "احمد محمد علي حسن", nationalId = "29912310123404", mobileNumber = "01012345678" };
    private static object B => new { fullName = "منى سمير علي حسن", nationalId = "30106152112354", mobileNumber = "01112345678" };

    // Published, open exam with MaxConcurrentAttempts = 1 and a 2-MCQ bank / selection of 1.
    private static async Task<Guid> CreateCappedExamAsync(HttpClient admin)
    {
        var topicResp = await admin.PostAsJsonAsync("/api/admin/topics", new { name = $"T{Guid.NewGuid():N}", displayOrder = 1 });
        topicResp.EnsureSuccessStatusCode();
        var topicId = (await topicResp.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        for (var i = 0; i < 2; i++)
        {
            (await admin.PostAsJsonAsync("/api/admin/questions", new
            {
                topicId, type = "Mcq", difficulty = "Medium", text = $"Q{i}",
                options = new[] { new { text = "a", isCorrect = false }, new { text = "b", isCorrect = true } }
            })).EnsureSuccessStatusCode();
        }
        var examResp = await admin.PostAsJsonAsync("/api/admin/exams", new
        {
            name = $"Exam {Guid.NewGuid():N}", description = (string?)null,
            startAtUtc = DateTime.UtcNow.AddMinutes(-5), endAtUtc = DateTime.UtcNow.AddHours(2),
            durationMinutes = 60, mcqPoints = 2m, trueFalsePoints = 1m, fillBlankPoints = 5m,
            passMarkPercentage = 60m, maxAttempts = 1, maxConcurrentAttempts = 1, graceWindowMinutes = 3,
            shuffleAnswers = true, showResultImmediately = true, allowBackNavigation = true,
            topicSelections = new[] { new { topicId, displayOrder = 1, difficulty = "Medium", type = "Mcq", count = 1 } }
        });
        examResp.EnsureSuccessStatusCode();
        var examId = (await examResp.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        (await admin.PostAsync($"/api/admin/exams/{examId}/publish", null)).EnsureSuccessStatusCode();
        return examId;
    }

    [Fact]
    public async Task SecondCandidate_IsQueued_ThenStartsAfterFirstSubmits()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreateCappedExamAsync(admin);
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        var startA = await (await clientA.PostAsJsonAsync($"/api/exam/{examId}/start", A)).Content.ReadFromJsonAsync<StartResponse>();
        Assert.Equal("Started", startA!.Outcome);

        var startB = await (await clientB.PostAsJsonAsync($"/api/exam/{examId}/start", B)).Content.ReadFromJsonAsync<StartResponse>();
        Assert.Equal("Queued", startB!.Outcome);
        Assert.Equal(1, startB.QueuePosition);

        // B polls: still waiting while A holds the only slot.
        var poll1 = await (await clientB.GetAsync($"/api/exam/{examId}/queue/status?nationalId=30106152112354")).Content.ReadFromJsonAsync<StatusResponse>();
        Assert.Equal("Waiting", poll1!.Status);

        // A submits -> frees the slot.
        clientA.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", startA.AttemptToken!);
        (await clientA.PostAsync($"/api/exam/{examId}/attempt/submit", null)).EnsureSuccessStatusCode();

        // B polls again -> promoted to Called.
        var poll2 = await (await clientB.GetAsync($"/api/exam/{examId}/queue/status?nationalId=30106152112354")).Content.ReadFromJsonAsync<StatusResponse>();
        Assert.Equal("Called", poll2!.Status);

        // B starts -> now gets an attempt.
        var startB2 = await (await clientB.PostAsJsonAsync($"/api/exam/{examId}/start", B)).Content.ReadFromJsonAsync<StartResponse>();
        Assert.Equal("Started", startB2!.Outcome);
    }

    private record IdResponse(Guid Id);
    private record StartResponse(string Outcome, Guid? AttemptId, string? AttemptToken, DateTime? ExpiresAtUtc, int? QueuePosition);
    private record StatusResponse(string Status, int Position, int EstimatedWaitSeconds);
}
