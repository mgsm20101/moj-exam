using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.CandidateExam.StartAttempt;

public class StartAttemptCommandHandler : IRequestHandler<StartAttemptCommand, Result<StartAttemptDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IQuestionSelectionService _selection;
    private readonly IAttemptTokenGenerator _tokens;
    private readonly IQueueReconciler _reconciler;

    public StartAttemptCommandHandler(
        IApplicationDbContext db, IQuestionSelectionService selection,
        IAttemptTokenGenerator tokens, IQueueReconciler reconciler)
    {
        _db = db;
        _selection = selection;
        _tokens = tokens;
        _reconciler = reconciler;
    }

    public async Task<Result<StartAttemptDto>> Handle(StartAttemptCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .Include(e => e.TopicSelections)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<StartAttemptDto>.Failure("Exam not found.");
        }

        var now = DateTime.UtcNow;
        if (!(exam.Status == ExamStatus.Published && now >= exam.StartAtUtc && now <= exam.EndAtUtc))
        {
            return Result<StartAttemptDto>.Failure("Exam is not open.");
        }

        NationalId.TryParse(request.NationalId, out var parsed, out _);
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.NationalId == request.NationalId, cancellationToken);
        if (candidate is null)
        {
            candidate = new Candidate
            {
                NationalId = request.NationalId,
                FullName = request.FullName.Trim(),
                MobileNumber = request.MobileNumber,
                BirthDateUtc = parsed!.BirthDateUtc,
                Gender = parsed.Gender,
                GovernorateCode = parsed.GovernorateCode
            };
            _db.Candidates.Add(candidate);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Resume: an in-progress attempt is returned as-is.
        var existing = await _db.ExamAttempts.FirstOrDefaultAsync(
            a => a.ExamId == exam.Id && a.CandidateId == candidate.Id && a.Status == ExamAttemptStatus.InProgress,
            cancellationToken);
        if (existing is not null)
        {
            return Result<StartAttemptDto>.Success(Token(existing, candidate.Id, exam.Id));
        }

        // Already taken (no active grant) blocks before any queueing.
        var hasAnyAttempt = await _db.ExamAttempts.AnyAsync(
            a => a.ExamId == exam.Id && a.CandidateId == candidate.Id, cancellationToken);
        var activeGrant = await _db.CandidateExamAttemptGrants.FirstOrDefaultAsync(
            g => g.ExamId == exam.Id && g.CandidateId == candidate.Id && g.IsActive, cancellationToken);
        if (hasAnyAttempt && activeGrant is null)
        {
            return Result<StartAttemptDto>.Failure("You have already taken this exam.");
        }

        // Batch gate.
        var capacity = await _reconciler.ReconcileAsync(exam.Id, cancellationToken);

        var called = await _db.WaitingQueueEntries.FirstOrDefaultAsync(
            e => e.ExamId == exam.Id && e.CandidateId == candidate.Id && e.Status == WaitingQueueStatus.Called,
            cancellationToken);

        // FR-8.7: in Manual mode only admin-Called candidates may enter; free capacity alone is not a ticket.
        if (called is not null || (exam.QueueMode == QueueMode.Auto && capacity.Available > 0))
        {
            var created = await CreateAttemptAsync(exam, candidate.Id, now, cancellationToken);
            if (!created.IsSuccess)
            {
                return Result<StartAttemptDto>.Failure(created.Errors);
            }

            // A retake grant is single-use: consume it now that it has actually let the candidate
            // start again, so it can't be reused for a third, fourth, ... attempt.
            if (hasAnyAttempt && activeGrant is not null)
            {
                activeGrant.IsActive = false;
            }

            // Mark any queue entry for this candidate as Started.
            var mine = await _db.WaitingQueueEntries.Where(
                e => e.ExamId == exam.Id && e.CandidateId == candidate.Id
                     && (e.Status == WaitingQueueStatus.Waiting || e.Status == WaitingQueueStatus.Called))
                .ToListAsync(cancellationToken);
            foreach (var entry in mine) { entry.Status = WaitingQueueStatus.Started; }
            await _db.SaveChangesAsync(cancellationToken);

            return Result<StartAttemptDto>.Success(Token(created.Value!, candidate.Id, exam.Id));
        }

        // Enqueue (idempotent).
        var waiting = await _db.WaitingQueueEntries.FirstOrDefaultAsync(
            e => e.ExamId == exam.Id && e.CandidateId == candidate.Id && e.Status == WaitingQueueStatus.Waiting,
            cancellationToken);
        if (waiting is null)
        {
            waiting = new WaitingQueueEntry
            {
                ExamId = exam.Id, CandidateId = candidate.Id, EnqueuedAtUtc = now,
                Status = WaitingQueueStatus.Waiting
            };
            _db.WaitingQueueEntries.Add(waiting);
            await _db.SaveChangesAsync(cancellationToken);
            await _reconciler.ReconcileAsync(exam.Id, cancellationToken); // assign position
            waiting = await _db.WaitingQueueEntries.FirstAsync(e => e.Id == waiting.Id, cancellationToken);
        }

        return Result<StartAttemptDto>.Success(StartAttemptDto.Queued(waiting.Position));
    }

    private async Task<Result<ExamAttempt>> CreateAttemptAsync(Exam exam, Guid candidateId, DateTime now, CancellationToken cancellationToken)
    {
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id,
            CandidateId = candidateId,
            StartedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(exam.DurationMinutes),
            Status = ExamAttemptStatus.InProgress
        };
        attempt.Seed = attempt.Id.GetHashCode();

        var snapshot = await _selection.BuildSnapshotAsync(exam, attempt.Seed, cancellationToken);
        if (!snapshot.IsSuccess)
        {
            return Result<ExamAttempt>.Failure(snapshot.Errors);
        }
        foreach (var q in snapshot.Value!) { attempt.Questions.Add(q); }

        _db.ExamAttempts.Add(attempt);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<ExamAttempt>.Success(attempt);
    }

    private StartAttemptDto Token(ExamAttempt attempt, Guid candidateId, Guid examId)
    {
        var token = _tokens.GenerateToken(attempt.Id, candidateId, examId, attempt.ExpiresAtUtc);
        return StartAttemptDto.Started(attempt.Id, token, attempt.ExpiresAtUtc);
    }
}
