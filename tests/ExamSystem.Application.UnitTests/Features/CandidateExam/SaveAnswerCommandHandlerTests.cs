using ExamSystem.Application.Features.CandidateExam.TakeExam;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Infrastructure.Grading;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class SaveAnswerCommandHandlerTests
{
    private static async Task<(Infrastructure.Persistence.ApplicationDbContext db, ExamAttempt attempt, AttemptQuestion q, Guid optId)>
        SeedAsync(DateTime expiresAtUtc)
    {
        var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "E", DurationMinutes = 60, McqPoints = 2m, PassMarkPercentage = 60m };
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
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5), ExpiresAtUtc = expiresAtUtc,
            Status = ExamAttemptStatus.InProgress
        };
        attempt.Questions.Add(q);
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, attempt, q, opt.Id);
    }

    [Fact]
    public async Task Handle_ValidMcqAnswer_CreatesAnswer()
    {
        var (db, attempt, q, optId) = await SeedAsync(DateTime.UtcNow.AddMinutes(30));
        var handler = new SaveAnswerCommandHandler(db, new AttemptGradingService());

        var result = await handler.Handle(
            new SaveAnswerCommand(attempt.Id, q.Id, optId, null, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = db.AttemptAnswers.Single();
        Assert.Equal(optId, saved.SelectedOptionId);
    }

    [Fact]
    public async Task Handle_ExistingAnswer_UpdatesInPlace()
    {
        var (db, attempt, q, optId) = await SeedAsync(DateTime.UtcNow.AddMinutes(30));
        db.AttemptAnswers.Add(new AttemptAnswer
        { AttemptId = attempt.Id, AttemptQuestionId = q.Id, SelectedOptionId = null, IsFlagged = false });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new SaveAnswerCommandHandler(db, new AttemptGradingService());
        var result = await handler.Handle(
            new SaveAnswerCommand(attempt.Id, q.Id, optId, null, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = db.AttemptAnswers.Single();
        Assert.Equal(optId, saved.SelectedOptionId);
        Assert.True(saved.IsFlagged);
    }

    [Fact]
    public async Task Handle_OptionNotInQuestion_Fails()
    {
        var (db, attempt, q, _) = await SeedAsync(DateTime.UtcNow.AddMinutes(30));
        var handler = new SaveAnswerCommandHandler(db, new AttemptGradingService());

        var result = await handler.Handle(
            new SaveAnswerCommand(attempt.Id, q.Id, Guid.NewGuid(), null, false), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ExpiredAttempt_AutoSubmitsAndFails()
    {
        var (db, attempt, q, optId) = await SeedAsync(DateTime.UtcNow.AddMinutes(-1));
        var handler = new SaveAnswerCommandHandler(db, new AttemptGradingService());

        var result = await handler.Handle(
            new SaveAnswerCommand(attempt.Id, q.Id, optId, null, false), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExamAttemptStatus.AutoSubmitted, db.ExamAttempts.Single().Status);
    }
}
