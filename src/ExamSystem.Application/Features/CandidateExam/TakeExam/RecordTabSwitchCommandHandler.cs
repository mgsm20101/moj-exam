using ExamSystem.Domain.Attempts;

namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public class RecordTabSwitchCommandHandler : IRequestHandler<RecordTabSwitchCommand, Result<bool>>
{
    private readonly IApplicationDbContext _db;

    public RecordTabSwitchCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(RecordTabSwitchCommand request, CancellationToken cancellationToken)
    {
        var attempt = await _db.ExamAttempts.FirstOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null)
        {
            return Result<bool>.Failure("Attempt not found.");
        }

        if (attempt.Status == ExamAttemptStatus.InProgress)
        {
            attempt.TabSwitchCount += 1;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}
