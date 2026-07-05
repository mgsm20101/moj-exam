using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.ArchiveExam;

public class ArchiveExamCommandHandler : IRequestHandler<ArchiveExamCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public ArchiveExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(ArchiveExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Closed)
        {
            return Result<Unit>.Failure("Only Closed exams can be archived.");
        }

        exam.Status = ExamStatus.Archived;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
