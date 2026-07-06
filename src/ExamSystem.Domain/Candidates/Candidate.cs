namespace ExamSystem.Domain.Candidates;

/// <summary>Permanent candidate profile, keyed by national ID and reused across exams (FR-1.5).</summary>
public class Candidate : BaseAuditableEntity
{
    public string NationalId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;

    public DateTime BirthDateUtc { get; set; }
    public Gender Gender { get; set; }
    public int GovernorateCode { get; set; }

    public ICollection<CandidateExamAttemptGrant> Grants { get; set; } = new List<CandidateExamAttemptGrant>();
}
