namespace ExamSystem.Application.Common.Interfaces;

public interface IAttemptTokenGenerator
{
    string GenerateToken(Guid attemptId, Guid candidateId, Guid examId, DateTime expiresAtUtc);
}
