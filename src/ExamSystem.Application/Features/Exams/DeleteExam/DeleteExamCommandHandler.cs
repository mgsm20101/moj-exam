using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.DeleteExam;

public class DeleteExamCommandHandler : IRequestHandler<DeleteExamCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public DeleteExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(DeleteExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Draft)
        {
            return Result<Unit>.Failure("Only Draft exams can be deleted -- archive it instead.");
        }

        _db.Exams.Remove(exam);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
