namespace ExamSystem.Application.Common.Interfaces;

/// <summary>
/// Lazy batch-gate reconciliation (FR-8): expire grace-timed-out Called reservations, promote the
/// earliest Waiting candidates while capacity allows (Auto mode only — Manual exams promote solely
/// via <see cref="CallNextBatchAsync"/>), and recompute Waiting positions. Runs on every
/// start / queue-status request; returns the post-reconciliation capacity. Persists its changes.
/// </summary>
public interface IQueueReconciler
{
    Task<QueueCapacity> ReconcileAsync(Guid examId, CancellationToken cancellationToken);

    /// <summary>
    /// Manual promotion primitive (FR-8.7): expire stale grace reservations, then promote the earliest
    /// Waiting entries capped by min(maxToCall, available capacity, waiting count). Returns how many
    /// were promoted to Called.
    /// </summary>
    Task<int> CallNextBatchAsync(Guid examId, int maxToCall, CancellationToken cancellationToken);
}
