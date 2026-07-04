namespace ExamSystem.Domain.Common;

/// <summary>Base type for all Domain entities; Id is client-generated (Guid) so it is available before persistence.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
