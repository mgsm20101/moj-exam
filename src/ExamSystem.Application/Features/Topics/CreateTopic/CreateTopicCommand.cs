namespace ExamSystem.Application.Features.Topics.CreateTopic;

public record CreateTopicCommand(string Name, int DisplayOrder) : IRequest<Result<Guid>>;
