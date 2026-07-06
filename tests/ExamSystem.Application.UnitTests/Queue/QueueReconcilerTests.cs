using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;
using ExamSystem.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamSystem.Application.UnitTests.Queue;

public class QueueReconcilerTests
{
    private static Exam Exam(int max, int grace = 3) =>
        new() { Name = "E", DurationMinutes = 60, MaxConcurrentAttempts = max, GraceWindowMinutes = grace };

    private static ExamAttempt InProgress(Guid examId, DateTime expiresAtUtc) => new()
    {
        ExamId = examId, CandidateId = Guid.NewGuid(),
        StartedAtUtc = DateTime.UtcNow.AddMinutes(-1), ExpiresAtUtc = expiresAtUtc,
        Status = ExamAttemptStatus.InProgress
    };

    private static WaitingQueueEntry Waiting(Guid examId, DateTime enqueuedAtUtc) => new()
    {
        ExamId = examId, CandidateId = Guid.NewGuid(), EnqueuedAtUtc = enqueuedAtUtc,
        Status = WaitingQueueStatus.Waiting
    };

    [Fact]
    public async Task Reconcile_FreeSlot_PromotesEarliestWaiting()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 1);
        db.Exams.Add(exam);
        var early = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-5));
        var late = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-2));
        db.WaitingQueueEntries.AddRange(late, early); // insert out of order on purpose
        await db.SaveChangesAsync(CancellationToken.None);

        var capacity = await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        var promoted = db.WaitingQueueEntries.Single(e => e.Status == WaitingQueueStatus.Called);
        Assert.Equal(early.Id, promoted.Id);                 // FIFO
        Assert.NotNull(promoted.CalledAtUtc);
        Assert.Equal(1, db.WaitingQueueEntries.Count(e => e.Status == WaitingQueueStatus.Waiting));
        Assert.Equal(0, capacity.Available);                 // slot now reserved
    }

    [Fact]
    public async Task Reconcile_ActiveAttemptFillsCapacity_NoPromotion()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 1);
        db.Exams.Add(exam);
        db.ExamAttempts.Add(InProgress(exam.Id, DateTime.UtcNow.AddMinutes(30)));
        db.WaitingQueueEntries.Add(Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync(CancellationToken.None);

        var capacity = await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        Assert.Equal(0, db.WaitingQueueEntries.Count(e => e.Status == WaitingQueueStatus.Called));
        Assert.Equal(1, capacity.ActiveAttempts);
    }

    [Fact]
    public async Task Reconcile_ExpiredAttempt_DoesNotCount_AndPromotes()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 1);
        db.Exams.Add(exam);
        db.ExamAttempts.Add(InProgress(exam.Id, DateTime.UtcNow.AddMinutes(-1))); // timer already expired
        db.WaitingQueueEntries.Add(Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync(CancellationToken.None);

        await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        Assert.Equal(1, db.WaitingQueueEntries.Count(e => e.Status == WaitingQueueStatus.Called));
    }

    [Fact]
    public async Task Reconcile_ExpiredCalledPastGrace_ReleasesSlotToNext()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 1, grace: 3);
        db.Exams.Add(exam);
        var stale = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-10));
        stale.Status = WaitingQueueStatus.Called;
        stale.CalledAtUtc = DateTime.UtcNow.AddMinutes(-5); // past 3-min grace
        var next = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-4));
        db.WaitingQueueEntries.AddRange(stale, next);
        await db.SaveChangesAsync(CancellationToken.None);

        await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        Assert.Equal(WaitingQueueStatus.Expired, db.WaitingQueueEntries.Single(e => e.Id == stale.Id).Status);
        Assert.Equal(WaitingQueueStatus.Called, db.WaitingQueueEntries.Single(e => e.Id == next.Id).Status);
    }

    [Fact]
    public async Task Reconcile_RecomputesWaitingPositions()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 0); // no capacity -> nothing promoted
        db.Exams.Add(exam);
        var first = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-5));
        var second = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-3));
        db.WaitingQueueEntries.AddRange(second, first);
        await db.SaveChangesAsync(CancellationToken.None);

        await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        Assert.Equal(1, db.WaitingQueueEntries.Single(e => e.Id == first.Id).Position);
        Assert.Equal(2, db.WaitingQueueEntries.Single(e => e.Id == second.Id).Position);
    }
}
