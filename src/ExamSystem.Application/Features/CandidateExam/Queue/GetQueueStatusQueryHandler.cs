using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.CandidateExam.Queue;

public class GetQueueStatusQueryHandler : IRequestHandler<GetQueueStatusQuery, Result<QueueStatusDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IQueueReconciler _reconciler;

    public GetQueueStatusQueryHandler(IApplicationDbContext db, IQueueReconciler reconciler)
    {
        _db = db;
        _reconciler = reconciler;
    }

    public async Task<Result<QueueStatusDto>> Handle(GetQueueStatusQuery request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<QueueStatusDto>.Failure("Exam not found.");
        }

        await _reconciler.ReconcileAsync(request.ExamId, cancellationToken);

        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.NationalId == request.NationalId, cancellationToken);
        if (candidate is null)
        {
            return Result<QueueStatusDto>.Success(new QueueStatusDto("NotQueued", 0, 0));
        }

        var entry = await _db.WaitingQueueEntries
            .Where(e => e.ExamId == request.ExamId && e.CandidateId == candidate.Id)
            .OrderByDescending(e => e.EnqueuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (entry is null)
        {
            return Result<QueueStatusDto>.Success(new QueueStatusDto("NotQueued", 0, 0));
        }

        var estimate = entry.Status == WaitingQueueStatus.Waiting
            ? (int)Math.Ceiling((double)entry.Position / Math.Max(1, exam.MaxConcurrentAttempts)) * exam.DurationMinutes * 60
            : 0;

        return Result<QueueStatusDto>.Success(new QueueStatusDto(entry.Status.ToString(), entry.Position, estimate));
    }
}
