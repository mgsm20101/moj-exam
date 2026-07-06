namespace ExamSystem.Application.Common.Models;

/// <summary>Post-reconciliation capacity snapshot for an exam.</summary>
public record QueueCapacity(int MaxConcurrent, int ActiveAttempts, int Reserved)
{
    public int Available => Math.Max(0, MaxConcurrent - ActiveAttempts - Reserved);
}
