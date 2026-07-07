using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.Exams.UpdateExam;

public record UpdateExamCommand(
    Guid Id,
    string Name,
    string? Description,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    int DurationMinutes,
    decimal McqPoints,
    decimal TrueFalsePoints,
    decimal FillBlankPoints,
    decimal PassMarkPercentage,
    int MaxAttempts,
    bool ShuffleAnswers,
    bool ShowResultImmediately,
    bool AllowBackNavigation,
    int MaxConcurrentAttempts,
    int GraceWindowMinutes,
    QueueMode QueueMode,
    List<ExamTopicSelectionInput> TopicSelections) : IRequest<Result<Unit>>;
