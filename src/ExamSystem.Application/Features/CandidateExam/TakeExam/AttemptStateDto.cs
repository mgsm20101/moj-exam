namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record AttemptOptionDto(Guid Id, string Text);

public record AttemptQuestionStateDto(
    Guid AttemptQuestionId,
    int DisplayOrder,
    string Type,
    string Text,
    string? ImageUrl,
    IReadOnlyList<AttemptOptionDto> Options,
    Guid? SelectedOptionId,
    string? AnswerText,
    bool IsFlagged);

public record AttemptStateDto(
    string Status,
    int RemainingSeconds,
    bool ShowResultImmediately,
    IReadOnlyList<AttemptQuestionStateDto> Questions);
