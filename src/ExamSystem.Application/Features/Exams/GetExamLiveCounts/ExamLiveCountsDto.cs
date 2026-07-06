namespace ExamSystem.Application.Features.Exams.GetExamLiveCounts;

/// <summary>Live batch-gate numbers for one Published exam (FR-8.8).</summary>
public record ExamLiveCountsDto(
    Guid ExamId,
    int ActiveAttempts,
    int MaxConcurrentAttempts,
    int ReservedCalled,
    int WaitingCount);
