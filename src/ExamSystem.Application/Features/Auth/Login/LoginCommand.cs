namespace ExamSystem.Application.Features.Auth.Login;

public record LoginCommand(string UserName, string Password) : IRequest<Result<LoginResponse>>
{
    public override string ToString() => $"LoginCommand {{ UserName = {UserName} }}";
}

public record LoginResponse(string Token, string UserName, IReadOnlyList<string> Roles);
