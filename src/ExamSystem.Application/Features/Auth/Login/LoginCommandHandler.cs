using ExamSystem.Application.Common.Interfaces;

namespace ExamSystem.Application.Features.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginCommandHandler(IIdentityService identityService, IJwtTokenGenerator jwtTokenGenerator)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validation = await _identityService.ValidateCredentialsAsync(request.UserName, request.Password);

        if (!validation.Succeeded || validation.UserId is null || validation.UserName is null)
        {
            return Result<LoginResponse>.Failure("Invalid username or password.");
        }

        var token = _jwtTokenGenerator.GenerateToken(validation.UserId, validation.UserName, validation.Roles);
        return Result<LoginResponse>.Success(new LoginResponse(token, validation.UserName, validation.Roles));
    }
}
