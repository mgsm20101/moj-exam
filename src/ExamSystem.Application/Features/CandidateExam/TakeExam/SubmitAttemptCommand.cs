namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record SubmitAttemptCommand(Guid AttemptId) : IRequest<Result<ResultDto>>;
