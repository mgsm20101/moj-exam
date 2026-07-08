namespace ExamSystem.Application.Common.Interfaces;

/// <summary>
/// Issues, rotates, and revokes persisted refresh tokens for the admin JWT flow. Implementations
/// store only a hash of each token and rotate on every use (issue-new + revoke-old).
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Creates a new refresh token for the user and returns the raw (unhashed) value.</summary>
    Task<string> IssueAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Validates and rotates the presented raw token: on success the old token is revoked and a new
    /// one is issued. Returns a failed result when the token is unknown, expired, or already revoked.
    /// </summary>
    Task<RefreshRotationResult> RotateAsync(string rawToken, CancellationToken cancellationToken);

    /// <summary>Revokes the presented raw token if it is currently active (idempotent otherwise).</summary>
    Task RevokeAsync(string rawToken, CancellationToken cancellationToken);
}

/// <summary>Outcome of a refresh-token rotation: the owning user and the freshly issued raw token.</summary>
public record RefreshRotationResult(bool Succeeded, string? UserId, string? NewRefreshToken)
{
    public static RefreshRotationResult Failure() => new(false, null, null);
}
