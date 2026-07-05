using ExamSystem.Application.Features.Topics.CreateTopic;
using ExamSystem.Application.Features.Topics.DeleteTopic;
using ExamSystem.Application.Features.Topics.GetTopics;
using ExamSystem.Application.Features.Topics.UpdateTopic;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Admin CRUD for exam Topics.</summary>
[ApiController]
[Route("api/admin/topics")]
[Authorize(Roles = "Admin")]
public class TopicsController : ControllerBase
{
    private readonly ISender _sender;

    public TopicsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTopicsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTopicCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(new { id = result.Value });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTopicRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateTopicCommand(id, request.Name, request.DisplayOrder, request.IsActive), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteTopicCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    public record UpdateTopicRequest(string Name, int DisplayOrder, bool IsActive);
}
