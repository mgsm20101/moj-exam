using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.ReopenExam;

/// <summary>
/// Reverses a <see cref="CloseExam.CloseExamCommand"/>: moves a Closed exam back to Published so
/// candidates can take it again ("stop / start" toggle, client note 6). Only Closed exams qualify —
/// a Draft is published via the normal flow, and an Archived exam is terminal.
/// </summary>
public class ReopenExamCommandHandler : IRequestHandler<ReopenExamCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public ReopenExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(ReopenExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Closed)
        {
            return Result<Unit>.Failure("Only Closed exams can be reopened.");
        }

        exam.Status = ExamStatus.Published;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
