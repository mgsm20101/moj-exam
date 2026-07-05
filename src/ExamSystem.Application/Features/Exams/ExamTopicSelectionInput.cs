using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Exams;

public record ExamTopicSelectionInput(Guid TopicId, int DisplayOrder, DifficultyLevel Difficulty, QuestionType Type, int Count);
