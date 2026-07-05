using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.CreateQuestion;

public record CreateQuestionCommand(
    Guid TopicId,
    QuestionType Type,
    DifficultyLevel Difficulty,
    string Text,
    string? ImageUrl,
    List<QuestionOptionInput>? Options,
    string? CorrectAnswerText,
    decimal? PointsOverride) : IRequest<Result<Guid>>;
