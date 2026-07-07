using ExamSystem.Domain.Candidates;

namespace ExamSystem.Application.Features.Candidates.GrantRetake;

/// <summary>FR-5.4: admin re-activation. Lets a candidate exceed the one-attempt-per-exam limit.</summary>
public class GrantCandidateRetakeCommandHandler : IRequestHandler<GrantCandidateRetakeCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public GrantCandidateRetakeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(GrantCandidateRetakeCommand request, CancellationToken cancellationToken)
    {
        var examExists = await _db.Exams.AnyAsync(e => e.Id == request.ExamId, cancellationToken);
        if (!examExists)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.NationalId == request.NationalId, cancellationToken);
        if (candidate is null)
        {
            return Result<Unit>.Failure("Candidate not found.");
        }

        var hasActiveGrant = await _db.CandidateExamAttemptGrants.AnyAsync(
            g => g.ExamId == request.ExamId && g.CandidateId == candidate.Id && g.IsActive, cancellationToken);
        if (hasActiveGrant)
        {
            return Result<Unit>.Success(Unit.Value);
        }

        _db.CandidateExamAttemptGrants.Add(new CandidateExamAttemptGrant
        {
            ExamId = request.ExamId,
            CandidateId = candidate.Id,
            IsActive = true
        });
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
