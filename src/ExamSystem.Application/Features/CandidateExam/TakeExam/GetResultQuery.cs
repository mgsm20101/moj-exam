namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record GetResultQuery(Guid AttemptId) : IRequest<Result<ResultDto>>;
