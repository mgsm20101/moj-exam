using ExamSystem.Application.Common.Interfaces;

namespace ExamSystem.Application.Features.Auth.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result<bool>>
{
    private readonly IRefreshTokenService _refreshTokenService;

    public LogoutCommandHandler(IRefreshTokenService refreshTokenService)
    {
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Revocation is idempotent: an unknown or already-revoked token is a no-op, so logout always
        // succeeds from the caller's perspective (never leaks whether the token existed).
        await _refreshTokenService.RevokeAsync(request.RefreshToken, cancellationToken);
        return Result<bool>.Success(true);
    }
}
