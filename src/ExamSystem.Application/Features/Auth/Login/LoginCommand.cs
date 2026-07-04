namespace ExamSystem.Application.Features.Auth.Login;

public record LoginCommand(string UserName, string Password) : IRequest<Result<LoginResponse>>;

public record LoginResponse(string Token, string UserName, IReadOnlyList<string> Roles);
