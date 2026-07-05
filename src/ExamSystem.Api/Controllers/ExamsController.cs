using ExamSystem.Application.Features.Exams;
using ExamSystem.Application.Features.Exams.ArchiveExam;
using ExamSystem.Application.Features.Exams.CloseExam;
using ExamSystem.Application.Features.Exams.CreateExam;
using ExamSystem.Application.Features.Exams.DeleteExam;
using ExamSystem.Application.Features.Exams.GetExamById;
using ExamSystem.Application.Features.Exams.GetExams;
using ExamSystem.Application.Features.Exams.PublishExam;
using ExamSystem.Application.Features.Exams.UpdateExam;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Admin CRUD and lifecycle management for Exams.</summary>
[ApiController]
[Route("api/admin/exams")]
[Authorize(Roles = "Admin")]
public class ExamsController : ControllerBase
{
    private readonly ISender _sender;

    public ExamsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetExamsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetExamByIdQuery(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(new { errors = result.Errors });
        }
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateExamCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(new { id = result.Value });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExamRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateExamCommand(
            id, request.Name, request.Description, request.StartAtUtc, request.EndAtUtc, request.DurationMinutes,
            request.McqPoints, request.TrueFalsePoints, request.FillBlankPoints, request.PassMarkPercentage,
            request.MaxAttempts, request.ShuffleAnswers, request.ShowResultImmediately, request.AllowBackNavigation,
            request.TopicSelections);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteExamCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new PublishExamCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CloseExamCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ArchiveExamCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    public record UpdateExamRequest(
        string Name, string? Description, DateTime StartAtUtc, DateTime EndAtUtc, int DurationMinutes,
        decimal McqPoints, decimal TrueFalsePoints, decimal FillBlankPoints, decimal PassMarkPercentage, int MaxAttempts,
        bool ShuffleAnswers, bool ShowResultImmediately, bool AllowBackNavigation,
        List<ExamTopicSelectionInput> TopicSelections);
}
