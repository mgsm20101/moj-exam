using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Infrastructure.BackgroundJobs;
using ExamSystem.Infrastructure.Grading;
using Xunit;

namespace ExamSystem.Application.UnitTests.BackgroundJobs;

public class ExpiredAttemptCloserTests
{
    private static (Infrastructure.Persistence.ApplicationDbContext db, Exam exam) NewDb()
    {
        var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "E", DurationMinutes = 60, McqPoints = 2m, PassMarkPercentage = 60m };
        db.Exams.Add(exam);
        return (db, exam);
    }

    private static ExamAttempt AnsweredAttempt(Exam exam, DateTime expiresAtUtc)
    {
        var opt = new AttemptQuestionOption { TextSnapshot = "A", IsCorrect = true, DisplayOrder = 1 };
        var q = new AttemptQuestion
        {
            DisplayOrder = 1, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium,
            TextSnapshot = "Q", Options = new List<AttemptQuestionOption> { opt }
        };
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-90), ExpiresAtUtc = expiresAtUtc,
            Status = ExamAttemptStatus.InProgress
        };
        attempt.Questions.Add(q);
        attempt.Answers.Add(new AttemptAnswer { AttemptId = attempt.Id, AttemptQuestionId = q.Id, SelectedOptionId = opt.Id });
        return attempt;
    }

    [Fact]
    public async Task CloseExpired_ExpiredInProgress_GradesAndAutoSubmits()
    {
        var (db, exam) = NewDb();
        db.ExamAttempts.Add(AnsweredAttempt(exam, DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync(CancellationToken.None);

        var closed = await new ExpiredAttemptCloser(db, new AttemptGradingService()).CloseExpiredAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1, closed);
        var attempt = db.ExamAttempts.Single();
        Assert.Equal(ExamAttemptStatus.AutoSubmitted, attempt.Status);
        Assert.Equal(2m, attempt.Score);
        Assert.NotNull(attempt.SubmittedAtUtc);
    }

    [Fact]
    public async Task CloseExpired_NotExpiredOrAlreadyClosed_AreUntouched()
    {
        var (db, exam) = NewDb();
        db.ExamAttempts.Add(AnsweredAttempt(exam, DateTime.UtcNow.AddMinutes(30))); // not expired
        var submitted = AnsweredAttempt(exam, DateTime.UtcNow.AddMinutes(-1));
        submitted.Status = ExamAttemptStatus.Submitted;                              // already closed
        db.ExamAttempts.Add(submitted);
        await db.SaveChangesAsync(CancellationToken.None);

        var closed = await new ExpiredAttemptCloser(db, new AttemptGradingService()).CloseExpiredAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(0, closed);
        Assert.Equal(0, db.ExamAttempts.Count(a => a.Status == ExamAttemptStatus.AutoSubmitted));
    }
}
