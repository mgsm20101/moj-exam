namespace ExamSystem.Domain.Attempts;

public class ExamAttempt : BaseAuditableEntity
{
    public Guid ExamId { get; set; }
    public Guid CandidateId { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }

    public ExamAttemptStatus Status { get; set; } = ExamAttemptStatus.InProgress;
    public decimal? Score { get; set; }

    /// <summary>Seed for deterministic question selection/shuffle (== Id); enables audit replay (FR-2 note).</summary>
    public int Seed { get; set; }

    /// <summary>Best-effort integrity signal (Slice 3): times the candidate left the exam tab.</summary>
    public int TabSwitchCount { get; set; }

    public ICollection<AttemptQuestion> Questions { get; set; } = new List<AttemptQuestion>();
    public ICollection<AttemptAnswer> Answers { get; set; } = new List<AttemptAnswer>();
}
