using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Auth.Login;

namespace ExamSystem.Application.Features.Auth.Refresh;

public class RefreshCommandHandler : IRequestHandler<RefreshCommand, Result<LoginResponse>>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public RefreshCommandHandler(
        IRefreshTokenService refreshTokenService,
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _refreshTokenService = refreshTokenService;
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<Result<LoginResponse>> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        var rotation = await _refreshTokenService.RotateAsync(request.RefreshToken, cancellationToken);
        if (!rotation.Succeeded || rotation.UserId is null || rotation.NewRefreshToken is null)
        {
            return Result<LoginResponse>.Failure("Invalid or expired refresh token.");
        }

        var userInfo = await _identityService.GetUserInfoAsync(rotation.UserId);
        if (userInfo is null)
        {
            return Result<LoginResponse>.Failure("Invalid or expired refresh token.");
        }

        var token = _jwtTokenGenerator.GenerateToken(userInfo.UserId, userInfo.UserName, userInfo.Roles);
        return Result<LoginResponse>.Success(
            new LoginResponse(token, rotation.NewRefreshToken, userInfo.UserName, userInfo.Roles));
    }
}
