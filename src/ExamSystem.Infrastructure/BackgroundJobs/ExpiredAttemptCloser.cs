using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Domain.Attempts;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Infrastructure.BackgroundJobs;

public class ExpiredAttemptCloser : IExpiredAttemptCloser
{
    private readonly IApplicationDbContext _db;
    private readonly IAttemptGradingService _grading;

    public ExpiredAttemptCloser(IApplicationDbContext db, IAttemptGradingService grading)
    {
        _db = db;
        _grading = grading;
    }

    public async Task<int> CloseExpiredAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var expired = await _db.ExamAttempts
            .Include(a => a.Questions).ThenInclude(q => q.Options)
            .Include(a => a.Answers)
            .Where(a => a.Status == ExamAttemptStatus.InProgress && a.ExpiresAtUtc <= nowUtc)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        var examIds = expired.Select(a => a.ExamId).Distinct().ToList();
        var exams = await _db.Exams.Where(e => examIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, cancellationToken);

        foreach (var attempt in expired)
        {
            if (!exams.TryGetValue(attempt.ExamId, out var exam))
            {
                continue;
            }
            var grade = _grading.Grade(attempt, exam);
            attempt.Score = grade.Score;
            attempt.Status = ExamAttemptStatus.AutoSubmitted;
            attempt.SubmittedAtUtc = attempt.ExpiresAtUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
