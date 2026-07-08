using System.Security.Cryptography;
using System.Text;
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamSystem.Infrastructure.Identity;

/// <summary>
/// Persists refresh tokens (hashed) and rotates them on every use. Raw tokens are 256 bits of
/// cryptographically strong randomness; only their SHA-256 hash is stored, and each successful
/// rotation revokes the presented token before issuing its replacement.
/// </summary>
public class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _db;
    private readonly JwtSettings _settings;

    public RefreshTokenService(ApplicationDbContext db, IOptions<JwtSettings> options)
    {
        _db = db;
        _settings = options.Value;
    }

    public async Task<string> IssueAsync(string userId, CancellationToken cancellationToken)
    {
        var (raw, hash) = GenerateToken();
        var now = DateTime.UtcNow;

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_settings.RefreshTokenDays)
        });

        await _db.SaveChangesAsync(cancellationToken);
        return raw;
    }

    public async Task<RefreshRotationResult> RotateAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return RefreshRotationResult.Failure();
        }

        var hash = Hash(rawToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        var now = DateTime.UtcNow;
        if (existing is null || !existing.IsActive(now))
        {
            return RefreshRotationResult.Failure();
        }

        var (newRaw, newHash) = GenerateToken();

        existing.RevokedAtUtc = now;
        existing.ReplacedByTokenHash = newHash;

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            TokenHash = newHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_settings.RefreshTokenDays)
        });

        await _db.SaveChangesAsync(cancellationToken);
        return new RefreshRotationResult(true, existing.UserId, newRaw);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var hash = Hash(rawToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is not null && existing.RevokedAtUtc is null)
        {
            existing.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static (string Raw, string Hash) GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var raw = Convert.ToBase64String(bytes);
        return (raw, Hash(raw));
    }

    private static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(bytes);
    }
}
