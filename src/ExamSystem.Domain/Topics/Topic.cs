namespace ExamSystem.Domain.Topics;

public class Topic : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
