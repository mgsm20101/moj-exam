namespace ExamSystem.Domain.Exams;

public class ExamTopicSelection : BaseEntity
{
    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    public Guid TopicId { get; set; }
    public Topic? Topic { get; set; }

    public int DisplayOrder { get; set; }
    public DifficultyLevel Difficulty { get; set; }
    public QuestionType Type { get; set; }
    public int Count { get; set; }
}
