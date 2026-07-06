namespace ExamSystem.Domain.Queue;

/// <summary>Batch-gate admission policy for an exam (FR-8.7).</summary>
public enum QueueMode
{
    /// <summary>Slots are released automatically; the reconciler promotes the queue (default).</summary>
    Auto = 0,

    /// <summary>No candidate enters without an explicit admin "open next batch" release.</summary>
    Manual = 1
}
