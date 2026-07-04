namespace ExamSystem.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<IdentityValidationResult> ValidateCredentialsAsync(string userName, string password);
}

public record IdentityValidationResult(bool Succeeded, string? UserId, string? UserName, IReadOnlyList<string> Roles)
{
    public static IdentityValidationResult Failure() => new(false, null, null, Array.Empty<string>());
}
