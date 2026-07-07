namespace ExamSystem.Application.Features.Candidates.GrantRetake;

public record GrantCandidateRetakeCommand(Guid ExamId, string NationalId) : IRequest<Result<Unit>>;
