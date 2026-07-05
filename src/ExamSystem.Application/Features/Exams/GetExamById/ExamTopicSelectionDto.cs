using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Exams.GetExamById;

public record ExamTopicSelectionDto(Guid TopicId, string TopicName, int DisplayOrder, DifficultyLevel Difficulty, QuestionType Type, int Count);
