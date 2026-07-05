using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.CloseExam;

public class CloseExamCommandHandler : IRequestHandler<CloseExamCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public CloseExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(CloseExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Published)
        {
            return Result<Unit>.Failure("Only Published exams can be closed.");
        }

        exam.Status = ExamStatus.Closed;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
