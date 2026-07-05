namespace ExamSystem.Domain.Exams;

public class Exam : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public int DurationMinutes { get; set; }

    /// <summary>Per-QuestionType grading (FR-4.4/4.12). A question's own PointsOverride wins when set.</summary>
    public decimal McqPoints { get; set; } = 2m;
    public decimal TrueFalsePoints { get; set; } = 1m;
    public decimal FillBlankPoints { get; set; } = 5m;

    public decimal PassMarkPercentage { get; set; } = 60m;
    public int MaxAttempts { get; set; } = 1;

    public bool ShuffleAnswers { get; set; } = true;
    public bool ShowResultImmediately { get; set; } = true;
    public bool AllowBackNavigation { get; set; } = true;

    public ExamStatus Status { get; set; } = ExamStatus.Draft;

    public ICollection<ExamTopicSelection> TopicSelections { get; set; } = new List<ExamTopicSelection>();
}
