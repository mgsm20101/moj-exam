using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.GetQuestionBankSummary;

public class GetQuestionBankSummaryQueryHandler : IRequestHandler<GetQuestionBankSummaryQuery, Result<List<QuestionBankSummaryRow>>>
{
    private readonly IApplicationDbContext _db;

    public GetQuestionBankSummaryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<QuestionBankSummaryRow>>> Handle(GetQuestionBankSummaryQuery request, CancellationToken cancellationToken)
    {
        var grouped = await _db.Questions
            .Where(q => q.IsActive)
            .Include(q => q.Topic)
            .GroupBy(q => new { q.Topic!.Name, q.Difficulty, q.Type })
            .Select(g => new { g.Key.Name, g.Key.Difficulty, g.Key.Type, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var rows = grouped
            .GroupBy(g => new { g.Name, g.Difficulty })
            .Select(g => new QuestionBankSummaryRow(
                g.Key.Name,
                g.Key.Difficulty,
                g.Where(x => x.Type == QuestionType.Mcq).Sum(x => x.Count),
                g.Where(x => x.Type == QuestionType.FillBlank).Sum(x => x.Count)))
            .OrderBy(r => r.TopicName).ThenBy(r => r.Difficulty)
            .ToList();

        return Result<List<QuestionBankSummaryRow>>.Success(rows);
    }
}
