namespace ExamSystem.Application.Features.Topics.GetTopics;

public record GetTopicsQuery : IRequest<Result<List<TopicDto>>>;
