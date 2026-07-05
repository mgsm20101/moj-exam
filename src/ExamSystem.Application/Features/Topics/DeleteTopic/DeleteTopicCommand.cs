namespace ExamSystem.Application.Features.Topics.DeleteTopic;

public record DeleteTopicCommand(Guid Id) : IRequest<Result<Unit>>;
