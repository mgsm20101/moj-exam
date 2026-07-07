namespace ExamSystem.Application.Features.Reports.GetExamResultsReport;

/// <summary>One candidate's best completed attempt for an exam, with the pass/fail decision resolved.</summary>
public record ExamResultRow(
    string FullName,
    string NationalId,
    string MobileNumber,
    decimal Score,
    decimal TotalPoints,
    decimal ScorePercentage,
    bool Passed,
    DateTime? SubmittedAtUtc,
    int GovernorateCode,
    int TabSwitchCount,
    bool HasActiveRetakeGrant);
