namespace ExamSystem.Application.Features.Reports.GetExamResultsReport;

/// <summary>Which candidates the results report should list. The summary always covers everyone regardless of this filter.</summary>
public enum ResultsFilter
{
    All = 0,
    Passed = 1,
    Failed = 2
}
