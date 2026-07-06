using ExamSystem.Application.Features.CandidateExam.TakeExam;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Infrastructure.Grading;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class GetAttemptStateQueryHandlerTests
{
    private static async Task<(Guid attemptId, Guid examId)> SeedInProgressAsync(
        Infrastructure.Persistence.ApplicationDbContext db, DateTime expiresAtUtc)
    {
        var exam = new Exam { Name = "E", DurationMinutes = 60, ShowResultImmediately = true,
            McqPoints = 2m, PassMarkPercentage = 60m };
        db.Exams.Add(exam);
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10), ExpiresAtUtc = expiresAtUtc,
            Status = ExamAttemptStatus.InProgress
        };
        var opt1 = new AttemptQuestionOption { TextSnapshot = "A", IsCorrect = true, DisplayOrder = 1 };
        var opt2 = new AttemptQuestionOption { TextSnapshot = "B", IsCorrect = false, DisplayOrder = 2 };
        var q = new AttemptQuestion
        {
            AttemptId = attempt.Id, DisplayOrder = 1, Type = QuestionType.Mcq,
            Difficulty = DifficultyLevel.Medium, TextSnapshot = "Q1",
            Options = new List<AttemptQuestionOption> { opt1, opt2 }
        };
        attempt.Questions.Add(q);
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(CancellationToken.None);
        return (attempt.Id, exam.Id);
    }

    [Fact]
    public async Task Handle_InProgress_ReturnsSanitizedQuestionsWithoutCorrectness()
    {
        using var db = TestDbContextFactory.Create();
        var (attemptId, _) = await SeedInProgressAsync(db, DateTime.UtcNow.AddMinutes(30));

        var handler = new GetAttemptStateQueryHandler(db, new AttemptGradingService());
        var result = await handler.Handle(new GetAttemptStateQuery(attemptId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("InProgress", result.Value!.Status);
        Assert.True(result.Value.RemainingSeconds > 0);
        var q = Assert.Single(result.Value.Questions);
        Assert.Equal(2, q.Options.Count);
        // sanitized: options expose only id + text (no correctness), and no correct-answer field exists on the DTO
        Assert.All(q.Options, o => Assert.False(string.IsNullOrEmpty(o.Text)));
    }

    [Fact]
    public async Task Handle_Expired_LazyAutoSubmits()
    {
        using var db = TestDbContextFactory.Create();
        var (attemptId, _) = await SeedInProgressAsync(db, DateTime.UtcNow.AddMinutes(-1));

        var handler = new GetAttemptStateQueryHandler(db, new AttemptGradingService());
        var result = await handler.Handle(new GetAttemptStateQuery(attemptId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("AutoSubmitted", result.Value!.Status);
        Assert.Equal(0, result.Value.RemainingSeconds);
        var saved = db.ExamAttempts.Single();
        Assert.Equal(ExamAttemptStatus.AutoSubmitted, saved.Status);
        Assert.NotNull(saved.SubmittedAtUtc);
    }
}
