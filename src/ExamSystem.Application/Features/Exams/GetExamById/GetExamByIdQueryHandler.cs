namespace ExamSystem.Application.Features.Exams.GetExamById;

public class GetExamByIdQueryHandler : IRequestHandler<GetExamByIdQuery, Result<ExamDetailDto>>
{
    private readonly IApplicationDbContext _db;

    public GetExamByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ExamDetailDto>> Handle(GetExamByIdQuery request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .Include(e => e.TopicSelections)
            .ThenInclude(s => s.Topic)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (exam is null)
        {
            return Result<ExamDetailDto>.Failure("Exam not found.");
        }

        var dto = new ExamDetailDto(
            exam.Id, exam.Name, exam.Description, exam.StartAtUtc, exam.EndAtUtc, exam.DurationMinutes,
            exam.McqPoints, exam.TrueFalsePoints, exam.FillBlankPoints, exam.PassMarkPercentage, exam.MaxAttempts,
            exam.ShuffleAnswers, exam.ShowResultImmediately, exam.AllowBackNavigation,
            exam.MaxConcurrentAttempts, exam.GraceWindowMinutes, exam.Status,
            exam.TopicSelections
                .OrderBy(s => s.DisplayOrder)
                .Select(s => new ExamTopicSelectionDto(s.TopicId, s.Topic!.Name, s.DisplayOrder, s.Difficulty, s.Type, s.Count))
                .ToList());

        return Result<ExamDetailDto>.Success(dto);
    }
}
