namespace ExamSystem.Application.Features.CandidateExam.RegisterCandidate;

public record RegisterCandidateCommand(
    Guid ExamId,
    string FullName,
    string NationalId,
    string MobileNumber) : IRequest<Result<RegisterResultDto>>;
