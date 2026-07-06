using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExamSystem.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ExamSystem.Infrastructure.Identity;

/// <summary>Issues a short-lived candidate attempt token (distinct from the admin JWT).</summary>
public class AttemptTokenGenerator : IAttemptTokenGenerator
{
    public const string AttemptIdClaim = "attempt_id";
    public const string CandidateIdClaim = "candidate_id";
    public const string ExamIdClaim = "exam_id";

    private readonly AttemptTokenSettings _settings;

    public AttemptTokenGenerator(IOptions<AttemptTokenSettings> options)
    {
        _settings = options.Value;
        if (string.IsNullOrWhiteSpace(_settings.Key) || Encoding.UTF8.GetByteCount(_settings.Key) < 32)
        {
            throw new InvalidOperationException("AttemptToken:Key must be configured and at least 32 bytes.");
        }
    }

    public string GenerateToken(Guid attemptId, Guid candidateId, Guid examId, DateTime expiresAtUtc)
    {
        var claims = new[]
        {
            new Claim(AttemptIdClaim, attemptId.ToString()),
            new Claim(CandidateIdClaim, candidateId.ToString()),
            new Claim(ExamIdClaim, examId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer, audience: _settings.Audience, claims: claims,
            expires: expiresAtUtc, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
