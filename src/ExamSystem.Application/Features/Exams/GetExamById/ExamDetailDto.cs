using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.Exams.GetExamById;

public record ExamDetailDto(
    Guid Id, string Name, string? Description, DateTime StartAtUtc, DateTime EndAtUtc, int DurationMinutes,
    decimal McqPoints, decimal TrueFalsePoints, decimal FillBlankPoints, decimal PassMarkPercentage, int MaxAttempts,
    bool ShuffleAnswers, bool ShowResultImmediately, bool AllowBackNavigation,
    int MaxConcurrentAttempts, int GraceWindowMinutes, QueueMode QueueMode, ExamStatus Status,
    List<ExamTopicSelectionDto> TopicSelections);
