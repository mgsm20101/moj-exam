namespace ExamSystem.Application.Common.Interfaces;

/// <summary>
/// Grades and closes every InProgress attempt whose timer has expired (FR-2.7 background path).
/// Returns the number of attempts closed. Shares its outcome with the lazy auto-submit in 1b.
/// </summary>
public interface IExpiredAttemptCloser
{
    Task<int> CloseExpiredAsync(DateTime nowUtc, CancellationToken cancellationToken);
}
