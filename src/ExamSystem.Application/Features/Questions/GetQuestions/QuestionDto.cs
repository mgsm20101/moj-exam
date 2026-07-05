using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.GetQuestions;

public record QuestionOptionDto(Guid Id, string Text, bool IsCorrect, int DisplayOrder);

public record QuestionDto(
    Guid Id,
    Guid TopicId,
    string TopicName,
    QuestionType Type,
    DifficultyLevel Difficulty,
    string Text,
    string? ImageUrl,
    string? CorrectAnswerText,
    decimal? PointsOverride,
    bool IsActive,
    List<QuestionOptionDto> Options);
