using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Reports.GetExamResultsReport;

/// <summary>
/// Computes the per-exam results report from completed attempts. Pass/fail reuses the exact grading
/// rule (<see cref="ExamSystem.Infrastructure.Grading.AttemptGradingService"/>): a candidate passes
/// when <c>score / total * 100 &gt;= PassMarkPercentage</c>. Because an exam's configuration is
/// immutable once published, <c>total</c> is constant per exam and is derived from the exam's
/// TopicSelections rather than loading each attempt's question snapshot.
/// </summary>
public class GetExamResultsReportQueryHandler
    : IRequestHandler<GetExamResultsReportQuery, Result<ExamResultsReportDto>>
{
    private readonly IApplicationDbContext _db;

    public GetExamResultsReportQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ExamResultsReportDto>> Handle(
        GetExamResultsReportQuery request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .Include(e => e.TopicSelections)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);

        if (exam is null)
        {
            return Result<ExamResultsReportDto>.Failure("Exam not found.");
        }

        var totalPoints = exam.TopicSelections.Sum(s => s.Count * PointsFor(exam, s.Type));
        var passMarkPoints = Math.Round(totalPoints * exam.PassMarkPercentage / 100m, 2);

        var completedAttempts = await _db.ExamAttempts
            .Where(a => a.ExamId == exam.Id
                && (a.Status == ExamAttemptStatus.Submitted || a.Status == ExamAttemptStatus.AutoSubmitted))
            .ToListAsync(cancellationToken);

        // One row per candidate = their best completed attempt (ties broken by the later submission).
        var bestPerCandidate = completedAttempts
            .GroupBy(a => a.CandidateId)
            .Select(g => g
                .OrderByDescending(a => a.Score ?? 0m)
                .ThenByDescending(a => a.SubmittedAtUtc ?? DateTime.MinValue)
                .First())
            .ToList();

        var candidateIds = bestPerCandidate.Select(a => a.CandidateId).ToList();
        var candidates = await _db.Candidates
            .Where(c => candidateIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var allRows = bestPerCandidate
            .Select(a =>
            {
                candidates.TryGetValue(a.CandidateId, out var candidate);
                var score = a.Score ?? 0m;
                // 'passed' uses the exact (unrounded) grading formula; the displayed percentage is
                // floored to 2 decimals so it can never round UP across the pass line and show, say,
                // "60%" next to a failed badge.
                var percentage = totalPoints > 0m ? Math.Truncate(score / totalPoints * 10000m) / 100m : 0m;
                var passed = totalPoints > 0m && score / totalPoints * 100m >= exam.PassMarkPercentage;

                return new ExamResultRow(
                    candidate?.FullName ?? string.Empty,
                    candidate?.NationalId ?? string.Empty,
                    candidate?.MobileNumber ?? string.Empty,
                    score,
                    totalPoints,
                    percentage,
                    passed,
                    a.SubmittedAtUtc,
                    candidate?.GovernorateCode ?? 0,
                    a.TabSwitchCount);
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.FullName)
            .ToList();

        var passedCount = allRows.Count(r => r.Passed);
        var totalCandidates = allRows.Count;
        var passRate = totalCandidates > 0
            ? Math.Round((decimal)passedCount / totalCandidates * 100m, 2)
            : 0m;

        var summary = new ExamResultsSummary(totalCandidates, passedCount, totalCandidates - passedCount, passRate);

        var filteredRows = request.Filter switch
        {
            ResultsFilter.Passed => allRows.Where(r => r.Passed).ToList(),
            ResultsFilter.Failed => allRows.Where(r => !r.Passed).ToList(),
            _ => allRows
        };

        var dto = new ExamResultsReportDto(
            exam.Id, exam.Name, totalPoints, exam.PassMarkPercentage, passMarkPoints,
            request.Filter, summary, filteredRows);

        return Result<ExamResultsReportDto>.Success(dto);
    }

    private static decimal PointsFor(Exam exam, QuestionType type) => type switch
    {
        QuestionType.Mcq => exam.McqPoints,
        QuestionType.TrueFalse => exam.TrueFalsePoints,
        QuestionType.FillBlank => exam.FillBlankPoints,
        _ => 0m
    };
}
