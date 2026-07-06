namespace ExamSystem.Application.Features.CandidateExam.RegisterCandidate;

public enum RegisterOutcome { CanStart, AlreadyTaken, NotOpen }

public record RegisterResultDto(RegisterOutcome Status, Guid CandidateId);
