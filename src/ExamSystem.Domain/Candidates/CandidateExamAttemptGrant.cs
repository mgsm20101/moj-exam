namespace ExamSystem.Domain.Candidates;

/// <summary>
/// Admin re-activation record (FR-5.4): an active grant lets a candidate exceed the
/// one-attempt-per-exam limit for a specific exam. Read-only in Slice 1a (written by admin later).
/// </summary>
public class CandidateExamAttemptGrant : BaseAuditableEntity
{
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }

    public Guid ExamId { get; set; }
    public bool IsActive { get; set; } = true;
}
