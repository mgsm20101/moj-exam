using ExamSystem.Application.Features.CandidateExam.GetExamLanding;
using ExamSystem.Application.Features.CandidateExam.RegisterCandidate;
using ExamSystem.Application.Features.CandidateExam.StartAttempt;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExamSystem.Api.Controllers;

/// <summary>Public candidate-facing exam entry (Slice 1a): landing, registration, start.</summary>
[ApiController]
[Route("api/exam")]
[AllowAnonymous]
[EnableRateLimiting("candidate")]
public class CandidateExamController : ControllerBase
{
    private readonly ISender _sender;

    public CandidateExamController(ISender sender) => _sender = sender;

    [HttpGet("{examId:guid}/landing")]
    public async Task<IActionResult> Landing(Guid examId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetExamLandingQuery(examId), cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(new { errors = result.Errors });
        }
        return Ok(result.Value);
    }

    [HttpPost("{examId:guid}/register")]
    public async Task<IActionResult> Register(Guid examId, [FromBody] CandidateIdentityRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterCandidateCommand(examId, request.FullName, request.NationalId, request.MobileNumber);
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(new { status = result.Value!.Status.ToString(), candidateId = result.Value.CandidateId });
    }

    [HttpPost("{examId:guid}/start")]
    public async Task<IActionResult> Start(Guid examId, [FromBody] CandidateIdentityRequest request, CancellationToken cancellationToken)
    {
        var command = new StartAttemptCommand(examId, request.FullName, request.NationalId, request.MobileNumber);
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(result.Value);
    }

    public record CandidateIdentityRequest(string FullName, string NationalId, string MobileNumber);
}
