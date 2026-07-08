namespace ExamSystem.Application.Features.Auth.Logout;

/// <summary>Revokes a refresh token so it can no longer be used to mint access tokens.</summary>
public record LogoutCommand(string RefreshToken) : IRequest<Result<bool>>
{
    public override string ToString() => "LogoutCommand";
}
