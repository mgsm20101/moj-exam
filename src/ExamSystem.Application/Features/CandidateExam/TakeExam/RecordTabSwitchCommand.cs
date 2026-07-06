namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record RecordTabSwitchCommand(Guid AttemptId) : IRequest<Result<bool>>;
