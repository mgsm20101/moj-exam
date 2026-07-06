using ExamSystem.Application.Features.CandidateExam.TakeExam;
using ExamSystem.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Candidate exam engine (Slice 1b), authenticated by the AttemptToken scheme.</summary>
[ApiController]
[Route("api/exam/{examId:guid}/attempt")]
[Authorize(AuthenticationSchemes = "AttemptToken")]
public class CandidateAttemptController : ControllerBase
{
    private readonly ISender _sender;

    public CandidateAttemptController(ISender sender) => _sender = sender;

    // Resolves the attempt id from the token and enforces it belongs to the route's exam.
    private IActionResult? Resolve(Guid examId, out Guid attemptId)
    {
        attemptId = Guid.Empty;
        var attemptClaim = User.FindFirst(AttemptTokenGenerator.AttemptIdClaim)?.Value;
        var examClaim = User.FindFirst(AttemptTokenGenerator.ExamIdClaim)?.Value;
        if (!Guid.TryParse(attemptClaim, out attemptId) || !Guid.TryParse(examClaim, out var tokenExamId)
            || tokenExamId != examId)
        {
            return Forbid();
        }
        return null;
    }

    [HttpGet("state")]
    public async Task<IActionResult> State(Guid examId, CancellationToken cancellationToken)
    {
        if (Resolve(examId, out var attemptId) is { } forbid) return forbid;
        var result = await _sender.Send(new GetAttemptStateQuery(attemptId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { errors = result.Errors });
    }

    [HttpPost("answer")]
    public async Task<IActionResult> Answer(Guid examId, [FromBody] SaveAnswerRequest request, CancellationToken cancellationToken)
    {
        if (Resolve(examId, out var attemptId) is { } forbid) return forbid;
        var result = await _sender.Send(
            new SaveAnswerCommand(attemptId, request.AttemptQuestionId, request.SelectedOptionId, request.AnswerText, request.IsFlagged),
            cancellationToken);
        return result.IsSuccess ? NoContent() : Conflict(new { errors = result.Errors });
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit(Guid examId, CancellationToken cancellationToken)
    {
        if (Resolve(examId, out var attemptId) is { } forbid) return forbid;
        var result = await _sender.Send(new SubmitAttemptCommand(attemptId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("result")]
    public async Task<IActionResult> Result(Guid examId, CancellationToken cancellationToken)
    {
        if (Resolve(examId, out var attemptId) is { } forbid) return forbid;
        var result = await _sender.Send(new GetResultQuery(attemptId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { errors = result.Errors });
    }

    public record SaveAnswerRequest(Guid AttemptQuestionId, Guid? SelectedOptionId, string? AnswerText, bool IsFlagged);
}
