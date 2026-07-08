using ExamSystem.Application.Features.Auth.Login;

namespace ExamSystem.Application.Features.Auth.Refresh;

/// <summary>Exchanges a valid refresh token for a fresh access-token / refresh-token pair.</summary>
public record RefreshCommand(string RefreshToken) : IRequest<Result<LoginResponse>>
{
    public override string ToString() => "RefreshCommand";
}
