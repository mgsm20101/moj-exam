using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Common.Models;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Queue;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Infrastructure.Queue;

public class QueueReconciler : IQueueReconciler
{
    private readonly IApplicationDbContext _db;

    public QueueReconciler(IApplicationDbContext db) => _db = db;

    public async Task<QueueCapacity> ReconcileAsync(Guid examId, CancellationToken cancellationToken)
    {
        var (capacity, _) = await ReconcileCoreAsync(examId, promoteLimit: null, cancellationToken);
        return capacity;
    }

    public async Task<int> CallNextBatchAsync(Guid examId, int maxToCall, CancellationToken cancellationToken)
    {
        var (_, called) = await ReconcileCoreAsync(examId, promoteLimit: Math.Max(0, maxToCall), cancellationToken);
        return called;
    }

    /// <summary>
    /// Shared reconciliation core. promoteLimit == null -> mode-driven promotion (Auto: unbounded,
    /// Manual: none). promoteLimit == n -> promote up to n (the manual batch button, FR-8.7).
    /// Capacity is always the hard cap.
    /// </summary>
    private async Task<(QueueCapacity Capacity, int Called)> ReconcileCoreAsync(
        Guid examId, int? promoteLimit, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var exam = await _db.Exams.FirstAsync(e => e.Id == examId, cancellationToken);

        // 1. Expire Called reservations whose grace window has elapsed.
        var called = await _db.WaitingQueueEntries
            .Where(e => e.ExamId == examId && e.Status == WaitingQueueStatus.Called)
            .ToListAsync(cancellationToken);
        foreach (var entry in called)
        {
            if (entry.CalledAtUtc is { } calledAt && calledAt.AddMinutes(exam.GraceWindowMinutes) <= now)
            {
                entry.Status = WaitingQueueStatus.Expired;
            }
        }

        var activeAttempts = await _db.ExamAttempts.CountAsync(
            a => a.ExamId == examId && a.Status == ExamAttemptStatus.InProgress && a.ExpiresAtUtc > now,
            cancellationToken);
        var reserved = called.Count(e => e.Status == WaitingQueueStatus.Called);

        // 2. Promote earliest Waiting while capacity allows, bounded by the promotion limit.
        var waiting = await _db.WaitingQueueEntries
            .Where(e => e.ExamId == examId && e.Status == WaitingQueueStatus.Waiting)
            .OrderBy(e => e.EnqueuedAtUtc)
            .ToListAsync(cancellationToken);

        var available = Math.Max(0, exam.MaxConcurrentAttempts - activeAttempts - reserved);
        var limit = promoteLimit ?? (exam.QueueMode == QueueMode.Auto ? int.MaxValue : 0);
        var toPromote = Math.Min(limit, Math.Min(available, waiting.Count));

        var index = 0;
        for (; index < toPromote; index++, available--, reserved++)
        {
            waiting[index].Status = WaitingQueueStatus.Called;
            waiting[index].CalledAtUtc = now;
        }

        // 3. Recompute positions for those still Waiting.
        var position = 1;
        for (var i = index; i < waiting.Count; i++)
        {
            waiting[i].Position = position++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (new QueueCapacity(exam.MaxConcurrentAttempts, activeAttempts, reserved), toPromote);
    }
}
