namespace ExamSystem.Application.Features.CandidateExam.StartAttempt;

/// <summary>Outcome of pressing "start": either an attempt was created, or the candidate was queued.</summary>
public record StartAttemptDto(
    string Outcome,               // "Started" | "Queued"
    Guid? AttemptId,
    string? AttemptToken,
    DateTime? ExpiresAtUtc,
    int? QueuePosition)
{
    public static StartAttemptDto Started(Guid attemptId, string token, DateTime expiresAtUtc) =>
        new("Started", attemptId, token, expiresAtUtc, null);

    public static StartAttemptDto Queued(int position) =>
        new("Queued", null, null, null, position);
}
