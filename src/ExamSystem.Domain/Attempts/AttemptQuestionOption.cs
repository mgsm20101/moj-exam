namespace ExamSystem.Domain.Attempts;

public class AttemptQuestionOption : BaseEntity
{
    public Guid AttemptQuestionId { get; set; }
    public string TextSnapshot { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }
}
