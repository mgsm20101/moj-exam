using ExamSystem.Application.Features.Auth.Login;
using ExamSystem.Application.Features.Auth.Logout;
using ExamSystem.Application.Features.Auth.Refresh;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Handles Admin authentication.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Authenticates an Admin user and returns a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Unauthorized(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    /// <summary>Exchanges a valid refresh token for a new access-token / refresh-token pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Unauthorized(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    /// <summary>Revokes a refresh token (sign-out). Always returns 200 so it never leaks token validity.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken cancellationToken)
    {
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }
}
