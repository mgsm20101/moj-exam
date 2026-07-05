using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.GetQuestions;

public record GetQuestionsQuery(Guid? TopicId, DifficultyLevel? Difficulty, bool? IsActive) : IRequest<Result<List<QuestionDto>>>;
