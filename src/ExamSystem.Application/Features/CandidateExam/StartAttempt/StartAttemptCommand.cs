namespace ExamSystem.Application.Features.CandidateExam.StartAttempt;

public record StartAttemptCommand(
    Guid ExamId,
    string FullName,
    string NationalId,
    string MobileNumber) : IRequest<Result<StartAttemptDto>>;
