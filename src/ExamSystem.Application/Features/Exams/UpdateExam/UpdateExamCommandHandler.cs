using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.UpdateExam;

public class UpdateExamCommandHandler : IRequestHandler<UpdateExamCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public UpdateExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(UpdateExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .Include(e => e.TopicSelections)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Draft)
        {
            return Result<Unit>.Failure("Only Draft exams can be edited.");
        }

        exam.Name = request.Name;
        exam.Description = request.Description;
        exam.StartAtUtc = request.StartAtUtc;
        exam.EndAtUtc = request.EndAtUtc;
        exam.DurationMinutes = request.DurationMinutes;
        exam.McqPoints = request.McqPoints;
        exam.TrueFalsePoints = request.TrueFalsePoints;
        exam.FillBlankPoints = request.FillBlankPoints;
        exam.PassMarkPercentage = request.PassMarkPercentage;
        exam.MaxAttempts = request.MaxAttempts;
        exam.ShuffleAnswers = request.ShuffleAnswers;
        exam.ShowResultImmediately = request.ShowResultImmediately;
        exam.AllowBackNavigation = request.AllowBackNavigation;
        exam.MaxConcurrentAttempts = request.MaxConcurrentAttempts;
        exam.GraceWindowMinutes = request.GraceWindowMinutes;
        exam.QueueMode = request.QueueMode;

        // Remove via the DbSet (not exam.TopicSelections.Clear()) and add replacements via the DbSet
        // as well: mutating the same tracked navigation collection for both the removal and the
        // re-add makes EF Core's change tracker misclassify the new rows as Modified instead of
        // Added (confirmed with EF Core 8 InMemory -- throws DbUpdateConcurrencyException on save).
        _db.ExamTopicSelections.RemoveRange(exam.TopicSelections);

        foreach (var selection in request.TopicSelections)
        {
            _db.ExamTopicSelections.Add(new ExamTopicSelection
            {
                ExamId = exam.Id,
                TopicId = selection.TopicId,
                DisplayOrder = selection.DisplayOrder,
                Difficulty = selection.Difficulty,
                Type = selection.Type,
                Count = selection.Count
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
