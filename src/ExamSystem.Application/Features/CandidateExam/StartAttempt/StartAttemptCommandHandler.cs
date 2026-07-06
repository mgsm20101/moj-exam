using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.CandidateExam.StartAttempt;

public class StartAttemptCommandHandler : IRequestHandler<StartAttemptCommand, Result<StartAttemptDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IQuestionSelectionService _selection;
    private readonly IAttemptTokenGenerator _tokens;

    public StartAttemptCommandHandler(
        IApplicationDbContext db, IQuestionSelectionService selection, IAttemptTokenGenerator tokens)
    {
        _db = db;
        _selection = selection;
        _tokens = tokens;
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

        // Resume: an in-progress attempt for this (candidate, exam) is returned as-is.
        var existing = await _db.ExamAttempts
            .FirstOrDefaultAsync(a => a.ExamId == exam.Id && a.CandidateId == candidate.Id
                                      && a.Status == ExamAttemptStatus.InProgress, cancellationToken);
        if (existing is not null)
        {
            return Ok(existing, candidate.Id, exam.Id);
        }

        // Otherwise: any prior attempt (without an active grant) blocks a new one (FR-1.5.1).
        var hasAnyAttempt = await _db.ExamAttempts
            .AnyAsync(a => a.ExamId == exam.Id && a.CandidateId == candidate.Id, cancellationToken);
        var hasActiveGrant = await _db.CandidateExamAttemptGrants
            .AnyAsync(g => g.ExamId == exam.Id && g.CandidateId == candidate.Id && g.IsActive, cancellationToken);
        if (hasAnyAttempt && !hasActiveGrant)
        {
            return Result<StartAttemptDto>.Failure("You have already taken this exam.");
        }

        var attempt = new ExamAttempt
        {
            ExamId = exam.Id,
            CandidateId = candidate.Id,
            StartedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(exam.DurationMinutes),
            Status = ExamAttemptStatus.InProgress
        };
        attempt.Seed = attempt.Id.GetHashCode();

        var snapshot = await _selection.BuildSnapshotAsync(exam, attempt.Seed, cancellationToken);
        if (!snapshot.IsSuccess)
        {
            return Result<StartAttemptDto>.Failure(snapshot.Errors);
        }
        foreach (var q in snapshot.Value!)
        {
            attempt.Questions.Add(q);
        }

        _db.ExamAttempts.Add(attempt);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(attempt, candidate.Id, exam.Id);
    }

    private Result<StartAttemptDto> Ok(ExamAttempt attempt, Guid candidateId, Guid examId)
    {
        var token = _tokens.GenerateToken(attempt.Id, candidateId, examId, attempt.ExpiresAtUtc);
        return Result<StartAttemptDto>.Success(new StartAttemptDto(attempt.Id, token, attempt.ExpiresAtUtc));
    }
}
