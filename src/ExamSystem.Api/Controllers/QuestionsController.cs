using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Questions.CreateQuestion;
using ExamSystem.Application.Features.Questions.DeleteQuestion;
using ExamSystem.Application.Features.Questions.GetQuestions;
using ExamSystem.Application.Features.Questions.UpdateQuestion;
using ExamSystem.Domain.Questions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Admin CRUD for the Question Bank, plus image upload.</summary>
[ApiController]
[Route("api/admin/questions")]
[Authorize(Roles = "Admin")]
public class QuestionsController : ControllerBase
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private readonly ISender _sender;
    private readonly IImageStorageService _imageStorage;

    public QuestionsController(ISender sender, IImageStorageService imageStorage)
    {
        _sender = sender;
        _imageStorage = imageStorage;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? topicId, [FromQuery] DifficultyLevel? difficulty, [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetQuestionsQuery(topicId, difficulty, isActive), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateQuestionCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(new { id = result.Value });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuestionRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateQuestionCommand(
            id, request.TopicId, request.Type, request.Difficulty, request.Text, request.ImageUrl,
            request.Options, request.CorrectAnswerText, request.PointsOverride, request.IsActive);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeactivateQuestionCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpPost("image")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "No file was uploaded." } });
        }
        if (file.Length > MaxImageSizeBytes)
        {
            return BadRequest(new { errors = new[] { "Image exceeds the 5 MB limit." } });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var url = await _imageStorage.SaveAsync(stream, file.FileName, file.ContentType, cancellationToken);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    public record UpdateQuestionRequest(
        Guid TopicId, QuestionType Type, DifficultyLevel Difficulty, string Text, string? ImageUrl,
        List<QuestionOptionInput>? Options, string? CorrectAnswerText, decimal? PointsOverride, bool IsActive);
}
