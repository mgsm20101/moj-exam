using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.Exams.GetExamLiveCounts;

/// <summary>
/// Read-only live counts per Published exam. Mirrors <see cref="Common.Interfaces.IQueueReconciler"/>
/// definitions arithmetically (grace-expired Called entries are excluded, not mutated) — this handler
/// must never write; reconciliation stays owned by the candidate-facing flow.
/// </summary>
public class GetExamLiveCountsQueryHandler : IRequestHandler<GetExamLiveCountsQuery, Result<List<ExamLiveCountsDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetExamLiveCountsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<ExamLiveCountsDto>>> Handle(GetExamLiveCountsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var published = await _db.Exams
            .Where(e => e.Status == ExamStatus.Published)
            .Select(e => new { e.Id, e.MaxConcurrentAttempts, e.GraceWindowMinutes })
            .ToListAsync(cancellationToken);

        if (published.Count == 0)
        {
            return Result<List<ExamLiveCountsDto>>.Success(new List<ExamLiveCountsDto>());
        }

        var examIds = published.Select(e => e.Id).ToList();

        var activeCounts = await _db.ExamAttempts
            .Where(a => examIds.Contains(a.ExamId) && a.Status == ExamAttemptStatus.InProgress && a.ExpiresAtUtc > now)
            .GroupBy(a => a.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ExamId, x => x.Count, cancellationToken);

        var waitingCounts = await _db.WaitingQueueEntries
            .Where(q => examIds.Contains(q.ExamId) && q.Status == WaitingQueueStatus.Waiting)
            .GroupBy(q => q.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ExamId, x => x.Count, cancellationToken);

        // Called entries need per-row CalledAtUtc because the grace window differs per exam.
        var calledEntries = await _db.WaitingQueueEntries
            .Where(q => examIds.Contains(q.ExamId) && q.Status == WaitingQueueStatus.Called)
            .Select(q => new { q.ExamId, q.CalledAtUtc })
            .ToListAsync(cancellationToken);

        var dtos = published
            .Select(e => new ExamLiveCountsDto(
                e.Id,
                activeCounts.GetValueOrDefault(e.Id),
                e.MaxConcurrentAttempts,
                calledEntries.Count(c =>
                    c.ExamId == e.Id &&
                    c.CalledAtUtc is { } calledAt &&
                    calledAt.AddMinutes(e.GraceWindowMinutes) > now),
                waitingCounts.GetValueOrDefault(e.Id)))
            .ToList();

        return Result<List<ExamLiveCountsDto>>.Success(dtos);
    }
}
