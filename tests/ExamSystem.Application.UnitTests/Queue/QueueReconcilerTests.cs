using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;
using ExamSystem.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamSystem.Application.UnitTests.Queue;

public class QueueReconcilerTests
{
    private static Exam Exam(int max, int grace = 3, QueueMode mode = QueueMode.Auto) =>
        new() { Name = "E", DurationMinutes = 60, MaxConcurrentAttempts = max, GraceWindowMinutes = grace, QueueMode = mode };

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

    [Fact]
    public async Task Reconcile_ManualMode_DoesNotPromote_ButStillExpiresAndRepositions()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 5, mode: QueueMode.Manual);
        db.Exams.Add(exam);
        var stale = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-10));
        stale.Status = WaitingQueueStatus.Called;
        stale.CalledAtUtc = DateTime.UtcNow.AddMinutes(-5); // past 3-min grace
        var first = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-4));
        var second = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-2));
        db.WaitingQueueEntries.AddRange(stale, second, first);
        await db.SaveChangesAsync(CancellationToken.None);

        var capacity = await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        // grace expiry still happens
        Assert.Equal(WaitingQueueStatus.Expired, db.WaitingQueueEntries.Single(e => e.Id == stale.Id).Status);
        // but nobody is promoted despite 5 free slots
        Assert.Equal(0, db.WaitingQueueEntries.Count(e => e.Status == WaitingQueueStatus.Called));
        // positions still honest
        Assert.Equal(1, db.WaitingQueueEntries.Single(e => e.Id == first.Id).Position);
        Assert.Equal(2, db.WaitingQueueEntries.Single(e => e.Id == second.Id).Position);
        Assert.Equal(5, capacity.Available);
    }

    [Fact]
    public async Task CallNextBatch_PromotesEarliest_CappedByCountAvailableAndWaiting()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 3, mode: QueueMode.Manual);
        db.Exams.Add(exam);
        db.ExamAttempts.Add(InProgress(exam.Id, DateTime.UtcNow.AddMinutes(30))); // 1 slot busy -> available = 2
        var first = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-9));
        var second = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-6));
        var third = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-3));
        db.WaitingQueueEntries.AddRange(third, first, second); // out of order on purpose
        await db.SaveChangesAsync(CancellationToken.None);

        var calledCount = await new QueueReconciler(db).CallNextBatchAsync(exam.Id, 10, CancellationToken.None);

        Assert.Equal(2, calledCount); // requested 10, capped by available (2)
        Assert.Equal(WaitingQueueStatus.Called, db.WaitingQueueEntries.Single(e => e.Id == first.Id).Status);
        Assert.Equal(WaitingQueueStatus.Called, db.WaitingQueueEntries.Single(e => e.Id == second.Id).Status);
        Assert.Equal(WaitingQueueStatus.Waiting, db.WaitingQueueEntries.Single(e => e.Id == third.Id).Status);
        Assert.Equal(1, db.WaitingQueueEntries.Single(e => e.Id == third.Id).Position); // repositioned
    }

    [Fact]
    public async Task CallNextBatch_ExpiresStaleGraceFirst_FreeingTheSlot()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 1, grace: 3, mode: QueueMode.Manual);
        db.Exams.Add(exam);
        var stale = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-10));
        stale.Status = WaitingQueueStatus.Called;
        stale.CalledAtUtc = DateTime.UtcNow.AddMinutes(-5); // past grace -> its slot must free up
        var next = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-4));
        db.WaitingQueueEntries.AddRange(stale, next);
        await db.SaveChangesAsync(CancellationToken.None);

        var calledCount = await new QueueReconciler(db).CallNextBatchAsync(exam.Id, 1, CancellationToken.None);

        Assert.Equal(1, calledCount);
        Assert.Equal(WaitingQueueStatus.Expired, db.WaitingQueueEntries.Single(e => e.Id == stale.Id).Status);
        Assert.Equal(WaitingQueueStatus.Called, db.WaitingQueueEntries.Single(e => e.Id == next.Id).Status);
    }

    [Fact]
    public async Task CallNextBatch_EmptyQueue_ReturnsZero()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 5, mode: QueueMode.Manual);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var calledCount = await new QueueReconciler(db).CallNextBatchAsync(exam.Id, 3, CancellationToken.None);

        Assert.Equal(0, calledCount);
    }
}
