using ExamSystem.Domain.Topics;

namespace ExamSystem.Domain.Questions;

public class Question : BaseAuditableEntity
{
    public Guid TopicId { get; set; }
    public Topic? Topic { get; set; }

    public string Text { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public QuestionType Type { get; set; }
    public DifficultyLevel Difficulty { get; set; }

    /// <summary>FillBlank only — always lowercase, single word, matches ^[a-z0-9]+$ (FR-3.2.1).</summary>
    public string? CorrectAnswerText { get; set; }

    public decimal? PointsOverride { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}
