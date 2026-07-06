namespace ExamSystem.Application.Features.CandidateExam.Queue;

public record QueueStatusDto(string Status, int Position, int EstimatedWaitSeconds);
