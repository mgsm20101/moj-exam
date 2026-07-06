using ExamSystem.Application.Features.Exams.GetExamLiveCounts;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class GetExamLiveCountsQueryHandlerTests
{
    private static Exam NewExam(string name, ExamStatus status) => new()
    {
        Name = name,
        StartAtUtc = DateTime.UtcNow.AddHours(-1),
        EndAtUtc = DateTime.UtcNow.AddDays(1),
        DurationMinutes = 60,
        MaxConcurrentAttempts = 20,
        GraceWindowMinutes = 3,
        Status = status
    };

    private static ExamAttempt NewAttempt(Guid examId, ExamAttemptStatus status, DateTime expiresAtUtc) => new()
    {
        ExamId = examId,
        CandidateId = Guid.NewGuid(),
        StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
        ExpiresAtUtc = expiresAtUtc,
        Status = status
    };

    private static WaitingQueueEntry NewQueueEntry(Guid examId, WaitingQueueStatus status, DateTime? calledAtUtc = null) => new()
    {
        ExamId = examId,
        CandidateId = Guid.NewGuid(),
        EnqueuedAtUtc = DateTime.UtcNow.AddMinutes(-5),
        Position = 0,
        Status = status,
        CalledAtUtc = calledAtUtc
    };

    [Fact]
    public async Task Handle_PublishedExam_ReturnsEffectiveCountsWithoutSideEffects()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam("Live", ExamStatus.Published);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var now = DateTime.UtcNow;
        // Attempts: 2 active, 1 expired-but-InProgress, 1 submitted.
        db.ExamAttempts.Add(NewAttempt(exam.Id, ExamAttemptStatus.InProgress, now.AddMinutes(30)));
        db.ExamAttempts.Add(NewAttempt(exam.Id, ExamAttemptStatus.InProgress, now.AddMinutes(30)));
        db.ExamAttempts.Add(NewAttempt(exam.Id, ExamAttemptStatus.InProgress, now.AddMinutes(-1)));
        db.ExamAttempts.Add(NewAttempt(exam.Id, ExamAttemptStatus.Submitted, now.AddMinutes(30)));
        // Queue: 2 waiting, 1 called within grace, 1 called past grace, 1 expired.
        db.WaitingQueueEntries.Add(NewQueueEntry(exam.Id, WaitingQueueStatus.Waiting));
        db.WaitingQueueEntries.Add(NewQueueEntry(exam.Id, WaitingQueueStatus.Waiting));
        db.WaitingQueueEntries.Add(NewQueueEntry(exam.Id, WaitingQueueStatus.Called, now.AddMinutes(-1)));
        var stale = NewQueueEntry(exam.Id, WaitingQueueStatus.Called, now.AddMinutes(-10));
        db.WaitingQueueEntries.Add(stale);
        db.WaitingQueueEntries.Add(NewQueueEntry(exam.Id, WaitingQueueStatus.Expired));
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamLiveCountsQueryHandler(db);
        var result = await handler.Handle(new GetExamLiveCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Value!);
        Assert.Equal(exam.Id, dto.ExamId);
        Assert.Equal(2, dto.ActiveAttempts);
        Assert.Equal(20, dto.MaxConcurrentAttempts);
        Assert.Equal(1, dto.ReservedCalled);   // the past-grace Called entry is excluded arithmetically
        Assert.Equal(2, dto.WaitingCount);

        // Read-only guarantee: the stale Called entry was NOT mutated to Expired.
        Assert.Equal(WaitingQueueStatus.Called, db.WaitingQueueEntries.Single(e => e.Id == stale.Id).Status);
    }

    [Fact]
    public async Task Handle_NonPublishedExams_AreNotReturned()
    {
        using var db = TestDbContextFactory.Create();
        db.Exams.Add(NewExam("Draft", ExamStatus.Draft));
        db.Exams.Add(NewExam("Closed", ExamStatus.Closed));
        db.Exams.Add(NewExam("Archived", ExamStatus.Archived));
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamLiveCountsQueryHandler(db);
        var result = await handler.Handle(new GetExamLiveCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Handle_PublishedExamWithNoActivity_ReturnsZeros()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam("Quiet", ExamStatus.Published);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamLiveCountsQueryHandler(db);
        var result = await handler.Handle(new GetExamLiveCountsQuery(), CancellationToken.None);

        var dto = Assert.Single(result.Value!);
        Assert.Equal(0, dto.ActiveAttempts);
        Assert.Equal(0, dto.ReservedCalled);
        Assert.Equal(0, dto.WaitingCount);
    }
}
