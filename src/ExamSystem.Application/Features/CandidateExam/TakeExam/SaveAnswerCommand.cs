namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record SaveAnswerCommand(
    Guid AttemptId,
    Guid AttemptQuestionId,
    Guid? SelectedOptionId,
    string? AnswerText,
    bool IsFlagged) : IRequest<Result<bool>>;
