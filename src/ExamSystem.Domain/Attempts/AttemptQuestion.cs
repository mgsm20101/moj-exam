using ExamSystem.Domain.Questions;

namespace ExamSystem.Domain.Attempts;

/// <summary>Immutable per-attempt snapshot of a presented question (FR-2.5).</summary>
public class AttemptQuestion : BaseEntity
{
    public Guid AttemptId { get; set; }
    public Guid SourceQuestionId { get; set; }
    public Guid TopicId { get; set; }

    public int DisplayOrder { get; set; }
    public QuestionType Type { get; set; }
    public DifficultyLevel Difficulty { get; set; }

    public string TextSnapshot { get; set; } = string.Empty;
    public string? ImageUrlSnapshot { get; set; }
    public string? CorrectAnswerTextSnapshot { get; set; }

    public ICollection<AttemptQuestionOption> Options { get; set; } = new List<AttemptQuestionOption>();
}
