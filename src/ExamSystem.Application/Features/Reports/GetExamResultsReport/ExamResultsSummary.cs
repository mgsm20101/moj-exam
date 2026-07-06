namespace ExamSystem.Application.Features.Reports.GetExamResultsReport;

/// <summary>Headline pass/fail counts for an exam, computed over all candidates (filter-independent).</summary>
public record ExamResultsSummary(
    int TotalCandidates,
    int PassedCount,
    int FailedCount,
    decimal PassRatePercentage);
