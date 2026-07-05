using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.CloneExam;

public class CloneExamCommandHandler : IRequestHandler<CloneExamCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;

    public CloneExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CloneExamCommand request, CancellationToken cancellationToken)
    {
        var source = await _db.Exams
            .Include(e => e.TopicSelections)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (source is null)
        {
            return Result<Guid>.Failure("Exam not found.");
        }

        var clone = new Exam
        {
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            StartAtUtc = source.StartAtUtc,
            EndAtUtc = source.EndAtUtc,
            DurationMinutes = source.DurationMinutes,
            McqPoints = source.McqPoints,
            TrueFalsePoints = source.TrueFalsePoints,
            FillBlankPoints = source.FillBlankPoints,
            PassMarkPercentage = source.PassMarkPercentage,
            MaxAttempts = source.MaxAttempts,
            ShuffleAnswers = source.ShuffleAnswers,
            ShowResultImmediately = source.ShowResultImmediately,
            AllowBackNavigation = source.AllowBackNavigation,
            Status = ExamStatus.Draft
        };

        foreach (var selection in source.TopicSelections)
        {
            clone.TopicSelections.Add(new ExamTopicSelection
            {
                TopicId = selection.TopicId,
                DisplayOrder = selection.DisplayOrder,
                Difficulty = selection.Difficulty,
                Type = selection.Type,
                Count = selection.Count
            });
        }

        _db.Exams.Add(clone);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(clone.Id);
    }
}
