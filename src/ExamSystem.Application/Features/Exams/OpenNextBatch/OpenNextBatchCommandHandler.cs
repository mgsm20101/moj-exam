using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.Exams.OpenNextBatch;

public class OpenNextBatchCommandHandler : IRequestHandler<OpenNextBatchCommand, Result<OpenBatchResultDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IQueueReconciler _reconciler;

    public OpenNextBatchCommandHandler(IApplicationDbContext db, IQueueReconciler reconciler)
    {
        _db = db;
        _reconciler = reconciler;
    }

    public async Task<Result<OpenBatchResultDto>> Handle(OpenNextBatchCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<OpenBatchResultDto>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Published)
        {
            return Result<OpenBatchResultDto>.Failure("Exam is not published.");
        }

        if (exam.QueueMode != QueueMode.Manual)
        {
            return Result<OpenBatchResultDto>.Failure("Exam queue is not in manual mode.");
        }

        var calledCount = await _reconciler.CallNextBatchAsync(request.ExamId, request.Count, cancellationToken);

        // Post-call numbers for the admin toast. CallNextBatchAsync just expired stale grace
        // reservations, so every remaining Called entry is within its grace window.
        var now = DateTime.UtcNow;
        var remainingWaiting = await _db.WaitingQueueEntries.CountAsync(
            e => e.ExamId == request.ExamId && e.Status == WaitingQueueStatus.Waiting, cancellationToken);
        var reserved = await _db.WaitingQueueEntries.CountAsync(
            e => e.ExamId == request.ExamId && e.Status == WaitingQueueStatus.Called, cancellationToken);
        var active = await _db.ExamAttempts.CountAsync(
            a => a.ExamId == request.ExamId && a.Status == ExamAttemptStatus.InProgress && a.ExpiresAtUtc > now,
            cancellationToken);
        var availableAfter = Math.Max(0, exam.MaxConcurrentAttempts - active - reserved);

        return Result<OpenBatchResultDto>.Success(new OpenBatchResultDto(calledCount, remainingWaiting, availableAfter));
    }
}
