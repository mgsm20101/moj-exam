using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.CreateExam;

public class CreateExamCommandHandler : IRequestHandler<CreateExamCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;

    public CreateExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateExamCommand request, CancellationToken cancellationToken)
    {
        var exam = new Exam
        {
            Name = request.Name,
            Description = request.Description,
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            DurationMinutes = request.DurationMinutes,
            McqPoints = request.McqPoints,
            TrueFalsePoints = request.TrueFalsePoints,
            FillBlankPoints = request.FillBlankPoints,
            PassMarkPercentage = request.PassMarkPercentage,
            MaxAttempts = request.MaxAttempts,
            ShuffleAnswers = request.ShuffleAnswers,
            ShowResultImmediately = request.ShowResultImmediately,
            AllowBackNavigation = request.AllowBackNavigation,
            Status = ExamStatus.Draft
        };

        foreach (var selection in request.TopicSelections)
        {
            exam.TopicSelections.Add(new ExamTopicSelection
            {
                TopicId = selection.TopicId,
                DisplayOrder = selection.DisplayOrder,
                Difficulty = selection.Difficulty,
                Type = selection.Type,
                Count = selection.Count
            });
        }

        _db.Exams.Add(exam);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(exam.Id);
    }
}
