namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record GetAttemptStateQuery(Guid AttemptId) : IRequest<Result<AttemptStateDto>>;
