using ExamSystem.Application.Features.CandidateExam.Queue;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExamSystem.Api.Controllers;

/// <summary>Public waiting-room polling (Slice 2). Identified by national ID so it survives disconnect (FR-8.6).</summary>
[ApiController]
[Route("api/exam/{examId:guid}/queue")]
[AllowAnonymous]
[EnableRateLimiting("candidate")]
public class CandidateQueueController : ControllerBase
{
    private readonly ISender _sender;

    public CandidateQueueController(ISender sender) => _sender = sender;

    [HttpGet("status")]
    public async Task<IActionResult> Status(Guid examId, [FromQuery] string nationalId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetQueueStatusQuery(examId, nationalId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { errors = result.Errors });
    }
}
