namespace ExamSystem.Domain.Exams;

/// <summary>One row = this exam needs `Count` active questions of `Type`/`Difficulty` from `Topic`.
/// `DisplayOrder` orders the topic within the exam and must match across every row of the same topic —
/// enforced by the CQRS layer, not the database.</summary>
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
