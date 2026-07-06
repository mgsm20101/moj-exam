namespace ExamSystem.Application.Features.Reports.GetExamResultsReport;

/// <summary>The full per-exam results report: headline summary plus the (filtered) candidate rows.</summary>
public record ExamResultsReportDto(
    Guid ExamId,
    string ExamName,
    decimal TotalPoints,
    decimal PassMarkPercentage,
    decimal PassMarkPoints,
    ResultsFilter Filter,
    ExamResultsSummary Summary,
    List<ExamResultRow> Rows);
