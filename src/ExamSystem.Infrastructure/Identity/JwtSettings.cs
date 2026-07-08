namespace ExamSystem.Infrastructure.Identity;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>Lifetime of a refresh token in days. Refresh tokens rotate on every use.</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
