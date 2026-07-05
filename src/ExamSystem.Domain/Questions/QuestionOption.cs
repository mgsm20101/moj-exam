namespace ExamSystem.Domain.Questions;

public class QuestionOption : BaseEntity
{
    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }

    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }
}
