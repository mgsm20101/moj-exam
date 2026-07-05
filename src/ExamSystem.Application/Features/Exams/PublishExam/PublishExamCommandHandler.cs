using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.PublishExam;

public class PublishExamCommandHandler : IRequestHandler<PublishExamCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public PublishExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(PublishExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .Include(e => e.TopicSelections)
            .ThenInclude(s => s.Topic)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Draft)
        {
            return Result<Unit>.Failure("Only Draft exams can be published.");
        }

        var errors = new List<string>();

        if (exam.StartAtUtc >= exam.EndAtUtc)
        {
            errors.Add("Start date must be before the end date.");
        }

        if (exam.EndAtUtc <= DateTime.UtcNow)
        {
            errors.Add("End date must be in the future.");
        }

        if (exam.TopicSelections.Count == 0)
        {
            errors.Add("Exam has no topics configured.");
        }

        foreach (var selection in exam.TopicSelections)
        {
            var available = await _db.Questions.CountAsync(
                q => q.TopicId == selection.TopicId && q.Difficulty == selection.Difficulty
                     && q.Type == selection.Type && q.IsActive,
                cancellationToken);

            if (available < selection.Count)
            {
                errors.Add(
                    $"Topic '{selection.Topic!.Name}' needs {selection.Count} {selection.Type}/{selection.Difficulty} " +
                    $"question(s) but only {available} are available.");
            }
        }

        if (errors.Count > 0)
        {
            return Result<Unit>.Failure(errors);
        }

        exam.Status = ExamStatus.Published;
        exam.ModifiedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
