using System.Text.RegularExpressions;
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Reports.GetAttemptReview;
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

    /// <summary>Per-question review of a single attempt (correct vs. the candidate's chosen answers).</summary>
    [HttpGet("attempts/{attemptId:guid}/review")]
    public async Task<IActionResult> GetAttemptReview(Guid attemptId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetAttemptReviewQuery(attemptId, EnforceShowResultGate: false, RevealCorrectAnswers: true),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(new { errors = result.Errors });
        }
        return Ok(result.Value);
    }

    private static ResultsFilter ParseFilter(string? filter) =>
        Enum.TryParse<ResultsFilter>(filter, ignoreCase: true, out var parsed) ? parsed : ResultsFilter.All;

    // OS-independent deny-list (Path.GetInvalidFileNameChars() returns only { '\0', '/' } on Linux/containers).
    private static readonly Regex UnsafeFileNameChars = new(@"[\\/:*?""<>|\x00-\x1F]", RegexOptions.Compiled);

    private static string Sanitize(string name)
    {
        var cleaned = UnsafeFileNameChars.Replace(name, "_").Trim().Trim('.', ' ');
        if (cleaned.Length > 100)
        {
            cleaned = cleaned[..100];
        }
        return string.IsNullOrWhiteSpace(cleaned) ? "exam" : cleaned;
    }
}
