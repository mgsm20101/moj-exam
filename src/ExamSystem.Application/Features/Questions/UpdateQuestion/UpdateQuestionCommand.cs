using ExamSystem.Application.Features.Questions.CreateQuestion;
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.UpdateQuestion;

public record UpdateQuestionCommand(
    Guid Id,
    Guid TopicId,
    QuestionType Type,
    DifficultyLevel Difficulty,
    string Text,
    string? ImageUrl,
    List<QuestionOptionInput>? Options,
    string? CorrectAnswerText,
    decimal? PointsOverride,
    bool IsActive) : IRequest<Result<Unit>>;
