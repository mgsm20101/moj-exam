namespace ExamSystem.Application.Features.CandidateExam.StartAttempt;

public record StartAttemptDto(Guid AttemptId, string AttemptToken, DateTime ExpiresAtUtc);
