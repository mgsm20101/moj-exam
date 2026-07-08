namespace ExamSystem.Infrastructure.Identity;

/// <summary>
/// A persisted, rotating refresh token bound to an <see cref="ApplicationUser"/>. Only the SHA-256
/// hash of the raw token is stored, so a database leak never exposes usable tokens. Each successful
/// refresh revokes the presented token and issues a new one (rotation), and the chain is recorded via
/// <see cref="ReplacedByTokenHash"/> so a reused/stolen token can be detected.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    /// <summary>The owning <see cref="ApplicationUser.Id"/>.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>SHA-256 (Base64) hash of the raw token; the raw value is never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Set when the token is revoked (either rotated out or on explicit logout).</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Hash of the token that superseded this one on rotation; null until rotated.</summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>A token is usable only while it is neither revoked nor past its expiry.</summary>
    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && nowUtc < ExpiresAtUtc;
}
