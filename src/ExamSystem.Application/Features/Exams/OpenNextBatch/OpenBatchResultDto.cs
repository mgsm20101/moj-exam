namespace ExamSystem.Application.Features.Exams.OpenNextBatch;

/// <summary>Outcome of an admin "open next batch" action (FR-8.7).</summary>
public record OpenBatchResultDto(int CalledCount, int RemainingWaiting, int AvailableAfter);
