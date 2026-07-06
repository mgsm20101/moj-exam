namespace ExamSystem.Application.Features.Reports.GetExamResultsReport;

/// <summary>Builds the pass/fail results report for a single exam (FR-6).</summary>
public record GetExamResultsReportQuery(Guid ExamId, ResultsFilter Filter = ResultsFilter.All)
    : IRequest<Result<ExamResultsReportDto>>;
