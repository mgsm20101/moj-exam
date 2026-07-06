using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.CandidateExam.RegisterCandidate;

public class RegisterCandidateCommandHandler : IRequestHandler<RegisterCandidateCommand, Result<RegisterResultDto>>
{
    private readonly IApplicationDbContext _db;

    public RegisterCandidateCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<RegisterResultDto>> Handle(RegisterCandidateCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<RegisterResultDto>.Failure("Exam not found.");
        }

        // Structural validity is guaranteed by the validator; parse again to derive fields.
        NationalId.TryParse(request.NationalId, out var parsed, out _);

        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.NationalId == request.NationalId, cancellationToken);

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

        var now = DateTime.UtcNow;
        var isOpen = exam.Status == ExamStatus.Published && now >= exam.StartAtUtc && now <= exam.EndAtUtc;
        if (!isOpen)
        {
            return Result<RegisterResultDto>.Success(new RegisterResultDto(RegisterOutcome.NotOpen, candidate.Id));
        }

        var hasAttempt = await _db.ExamAttempts
            .AnyAsync(a => a.ExamId == exam.Id && a.CandidateId == candidate.Id, cancellationToken);
        var hasActiveGrant = await _db.CandidateExamAttemptGrants
            .AnyAsync(g => g.ExamId == exam.Id && g.CandidateId == candidate.Id && g.IsActive, cancellationToken);

        var outcome = hasAttempt && !hasActiveGrant ? RegisterOutcome.AlreadyTaken : RegisterOutcome.CanStart;
        return Result<RegisterResultDto>.Success(new RegisterResultDto(outcome, candidate.Id));
    }
}
