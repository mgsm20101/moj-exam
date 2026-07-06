namespace ExamSystem.Application.Features.Exams.CreateExam;

public record CreateExamCommand(
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
    List<ExamTopicSelectionInput> TopicSelections) : IRequest<Result<Guid>>;
