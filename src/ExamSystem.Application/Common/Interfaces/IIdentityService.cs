namespace ExamSystem.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<IdentityValidationResult> ValidateCredentialsAsync(string userName, string password);

    /// <summary>Resolves a user's name and roles by id, used when minting a new access token on refresh.</summary>
    Task<IdentityUserInfo?> GetUserInfoAsync(string userId);
}

public record IdentityValidationResult(bool Succeeded, string? UserId, string? UserName, IReadOnlyList<string> Roles)
{
    public static IdentityValidationResult Failure() => new(false, null, null, Array.Empty<string>());
}

public record IdentityUserInfo(string UserId, string UserName, IReadOnlyList<string> Roles);
