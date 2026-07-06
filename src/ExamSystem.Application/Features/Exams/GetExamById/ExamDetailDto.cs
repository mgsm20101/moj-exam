using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.GetExamById;

public record ExamDetailDto(
    Guid Id, string Name, string? Description, DateTime StartAtUtc, DateTime EndAtUtc, int DurationMinutes,
    decimal McqPoints, decimal TrueFalsePoints, decimal FillBlankPoints, decimal PassMarkPercentage, int MaxAttempts,
    bool ShuffleAnswers, bool ShowResultImmediately, bool AllowBackNavigation,
    int MaxConcurrentAttempts, int GraceWindowMinutes, ExamStatus Status,
    List<ExamTopicSelectionDto> TopicSelections);
