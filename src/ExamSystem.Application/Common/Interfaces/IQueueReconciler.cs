namespace ExamSystem.Application.Common.Interfaces;

/// <summary>
/// Lazy batch-gate reconciliation (FR-8): expire grace-timed-out Called reservations, promote the
/// earliest Waiting candidates while capacity allows, and recompute Waiting positions. Runs on every
/// start / queue-status request; returns the post-reconciliation capacity. Persists its changes.
/// </summary>
public interface IQueueReconciler
{
    Task<QueueCapacity> ReconcileAsync(Guid examId, CancellationToken cancellationToken);
}
