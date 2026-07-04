namespace ExamSystem.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(string userId, string userName, IReadOnlyList<string> roles);
}
