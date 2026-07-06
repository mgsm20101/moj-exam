using ExamSystem.Application.Features.CandidateExam.TakeExam;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Infrastructure.Grading;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class SubmitAttemptCommandHandlerTests
{
    private static async Task<(Infrastructure.Persistence.ApplicationDbContext db, ExamAttempt attempt)>
        SeedAnsweredAsync(bool showResult)
    {
        var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "E", DurationMinutes = 60, McqPoints = 2m, PassMarkPercentage = 60m,
            ShowResultImmediately = showResult };
        db.Exams.Add(exam);
        var opt = new AttemptQuestionOption { TextSnapshot = "A", IsCorrect = true, DisplayOrder = 1 };
        var q = new AttemptQuestion
        {
            DisplayOrder = 1, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium,
            TextSnapshot = "Q", Options = new List<AttemptQuestionOption> { opt }
        };
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            Status = ExamAttemptStatus.InProgress
        };
        attempt.Questions.Add(q);
        attempt.Answers.Add(new AttemptAnswer { AttemptId = attempt.Id, AttemptQuestionId = q.Id, SelectedOptionId = opt.Id });
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, attempt);
    }

    [Fact]
    public async Task Handle_Submit_GradesAndMarksSubmitted()
    {
        var (db, attempt) = await SeedAnsweredAsync(showResult: true);
        var handler = new SubmitAttemptCommandHandler(db, new AttemptGradingService());

        var result = await handler.Handle(new SubmitAttemptCommand(attempt.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Shown);
        Assert.Equal(2m, result.Value.Score);
        Assert.True(result.Value.Passed);
        Assert.Equal(ExamAttemptStatus.Submitted, db.ExamAttempts.Single().Status);
    }

    [Fact]
    public async Task Handle_SubmitTwice_IsIdempotent()
    {
        var (db, attempt) = await SeedAnsweredAsync(showResult: true);
        var handler = new SubmitAttemptCommandHandler(db, new AttemptGradingService());

        var first = await handler.Handle(new SubmitAttemptCommand(attempt.Id), CancellationToken.None);
        var submittedAt = db.ExamAttempts.Single().SubmittedAtUtc;
        var second = await handler.Handle(new SubmitAttemptCommand(attempt.Id), CancellationToken.None);

        Assert.Equal(first.Value!.Score, second.Value!.Score);
        Assert.Equal(submittedAt, db.ExamAttempts.Single().SubmittedAtUtc);
    }

    [Fact]
    public async Task Handle_ResultWithheld_WhenShowResultImmediatelyFalse()
    {
        var (db, attempt) = await SeedAnsweredAsync(showResult: false);
        var handler = new SubmitAttemptCommandHandler(db, new AttemptGradingService());

        var result = await handler.Handle(new SubmitAttemptCommand(attempt.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Shown);
    }
}
