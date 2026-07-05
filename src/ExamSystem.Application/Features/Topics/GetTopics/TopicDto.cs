namespace ExamSystem.Application.Features.Topics.GetTopics;

public record TopicDto(Guid Id, string Name, int DisplayOrder, bool IsActive, int QuestionCount);
