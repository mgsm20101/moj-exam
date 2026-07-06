namespace ExamSystem.Application.Features.CandidateExam.Queue;

public record GetQueueStatusQuery(Guid ExamId, string NationalId) : IRequest<Result<QueueStatusDto>>;
