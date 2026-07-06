using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Reports.GetExamResultsReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Admin reporting: per-exam pass/fail results, on screen and as an Excel export (FR-6).</summary>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly ISender _sender;
    private readonly IExcelReportWriter _reportWriter;

    public ReportsController(ISender sender, IExcelReportWriter reportWriter)
    {
        _sender = sender;
        _reportWriter = reportWriter;
    }

    [HttpGet("exams/{examId:guid}/results")]
    public async Task<IActionResult> GetExamResults(
        Guid examId, [FromQuery] string? filter, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetExamResultsReportQuery(examId, ParseFilter(filter)), cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(new { errors = result.Errors });
        }
        return Ok(result.Value);
    }

    [HttpGet("exams/{examId:guid}/results/export")]
    public async Task<IActionResult> ExportExamResults(
        Guid examId, [FromQuery] string? filter, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetExamResultsReportQuery(examId, ParseFilter(filter)), cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(new { errors = result.Errors });
        }

        var bytes = _reportWriter.WriteExamResults(result.Value!);
        var fileName = $"{Sanitize(result.Value!.ExamName)} - Results.xlsx";
        return File(bytes, XlsxContentType, fileName);
    }

    private static ResultsFilter ParseFilter(string? filter) =>
        Enum.TryParse<ResultsFilter>(filter, ignoreCase: true, out var parsed) ? parsed : ResultsFilter.All;

    private static string Sanitize(string name)
    {
        var cleaned = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(cleaned) ? "exam" : cleaned.Trim();
    }
}
