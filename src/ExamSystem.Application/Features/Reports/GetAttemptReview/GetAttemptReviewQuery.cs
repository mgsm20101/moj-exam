namespace ExamSystem.Application.Features.Reports.GetAttemptReview;

/// <summary>
/// Loads the per-question review of a submitted attempt.
/// <para><paramref name="EnforceShowResultGate"/> is true for the candidate-facing flow (result
/// withheld unless the exam allows it) and false for the admin flow (always visible).</para>
/// <para><paramref name="RevealCorrectAnswers"/> is true for admins (they see which answer is
/// correct) and false for candidates (they see only whether their own answer was right or wrong —
/// the correct answer is never sent to their browser).</para>
/// </summary>
public record GetAttemptReviewQuery(Guid AttemptId, bool EnforceShowResultGate, bool RevealCorrectAnswers)
    : IRequest<Result<AttemptReviewDto>>;
