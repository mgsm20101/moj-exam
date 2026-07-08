using ExamSystem.Application.Common.Interfaces;

namespace ExamSystem.Application.Features.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IRefreshTokenService _refreshTokenService;

    public LoginCommandHandler(
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenService refreshTokenService)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validation = await _identityService.ValidateCredentialsAsync(request.UserName, request.Password);

        if (!validation.Succeeded || validation.UserId is null || validation.UserName is null)
        {
            return Result<LoginResponse>.Failure("Invalid username or password.");
        }

        var token = _jwtTokenGenerator.GenerateToken(validation.UserId, validation.UserName, validation.Roles);
        var refreshToken = await _refreshTokenService.IssueAsync(validation.UserId, cancellationToken);
        return Result<LoginResponse>.Success(new LoginResponse(token, refreshToken, validation.UserName, validation.Roles));
    }
}
