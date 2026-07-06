using ExamSystem.Application.Features.CandidateExam.TakeExam;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class RecordTabSwitchCommandHandlerTests
{
    private static async Task<(Infrastructure.Persistence.ApplicationDbContext db, ExamAttempt attempt)> SeedAsync(ExamAttemptStatus status)
    {
        var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "E", DurationMinutes = 60 };
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            Status = status
        };
        db.Exams.Add(exam);
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, attempt);
    }

    [Fact]
    public async Task Handle_InProgress_IncrementsCount()
    {
        var (db, attempt) = await SeedAsync(ExamAttemptStatus.InProgress);
        var handler = new RecordTabSwitchCommandHandler(db);

        await handler.Handle(new RecordTabSwitchCommand(attempt.Id), CancellationToken.None);
        await handler.Handle(new RecordTabSwitchCommand(attempt.Id), CancellationToken.None);

        Assert.Equal(2, db.ExamAttempts.Single().TabSwitchCount);
    }

    [Fact]
    public async Task Handle_NotInProgress_DoesNotIncrement()
    {
        var (db, attempt) = await SeedAsync(ExamAttemptStatus.Submitted);
        var handler = new RecordTabSwitchCommandHandler(db);

        var result = await handler.Handle(new RecordTabSwitchCommand(attempt.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, db.ExamAttempts.Single().TabSwitchCount);
    }
}
