using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Exams.GetExams;

public class GetExamsQueryHandler : IRequestHandler<GetExamsQuery, Result<List<ExamSummaryDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetExamsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<ExamSummaryDto>>> Handle(GetExamsQuery request, CancellationToken cancellationToken)
    {
        var exams = await _db.Exams.Include(e => e.TopicSelections).ToListAsync(cancellationToken);

        var dtos = exams
            .Select(e => new ExamSummaryDto(
                e.Id, e.Name, e.StartAtUtc, e.EndAtUtc, e.DurationMinutes, e.Status,
                e.TopicSelections.Sum(s => s.Count),
                e.TopicSelections.Sum(s => s.Count * PointsFor(e, s.Type))))
            .OrderByDescending(d => d.StartAtUtc)
            .ToList();

        return Result<List<ExamSummaryDto>>.Success(dtos);
    }

    private static decimal PointsFor(Exam exam, QuestionType type) => type switch
    {
        QuestionType.Mcq => exam.McqPoints,
        QuestionType.TrueFalse => exam.TrueFalsePoints,
        QuestionType.FillBlank => exam.FillBlankPoints,
        _ => 0m
    };
}
