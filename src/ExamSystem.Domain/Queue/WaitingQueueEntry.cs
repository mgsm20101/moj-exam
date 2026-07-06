namespace ExamSystem.Domain.Queue;

/// <summary>A candidate's place in an exam's FIFO batch-gate queue (FR-8), keyed by candidate per exam.</summary>
public class WaitingQueueEntry : BaseEntity
{
    public Guid ExamId { get; set; }
    public Guid CandidateId { get; set; }

    public DateTime EnqueuedAtUtc { get; set; }
    public int Position { get; set; }
    public DateTime? CalledAtUtc { get; set; }

    public WaitingQueueStatus Status { get; set; } = WaitingQueueStatus.Waiting;
}
