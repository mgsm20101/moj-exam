namespace ExamSystem.Application.Features.Topics.UpdateTopic;

public record UpdateTopicCommand(Guid Id, string Name, int DisplayOrder, bool IsActive) : IRequest<Result<Unit>>;
