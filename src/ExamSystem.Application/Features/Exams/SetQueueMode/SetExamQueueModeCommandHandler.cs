using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.SetQueueMode;

public class SetExamQueueModeCommandHandler : IRequestHandler<SetExamQueueModeCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public SetExamQueueModeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(SetExamQueueModeCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status is not (ExamStatus.Draft or ExamStatus.Published))
        {
            return Result<Unit>.Failure("Queue mode can only be changed for draft or published exams.");
        }

        exam.QueueMode = request.Mode;
        exam.ModifiedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
