using ExamSystem.Application.Features.Reports.GetExamResultsReport;

namespace ExamSystem.Application.Common.Interfaces;

/// <summary>Writes admin reports to an Excel (.xlsx) workbook. Mirrors <see cref="IExcelQuestionParser"/> (read side).</summary>
public interface IExcelReportWriter
{
    /// <summary>Renders the exam results report (summary + filtered rows) as an .xlsx byte array.</summary>
    byte[] WriteExamResults(ExamResultsReportDto report);
}
