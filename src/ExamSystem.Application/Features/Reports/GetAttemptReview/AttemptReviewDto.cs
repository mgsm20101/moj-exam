using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Reports.GetAttemptReview;

/// <summary>
/// A full per-question review of a completed attempt: the presented question snapshots, the
/// candidate's chosen answers, and which answer was correct. When <see cref="Shown"/> is false
/// (candidate flow with ShowResultImmediately == false) the question list is withheld.
/// </summary>
public record AttemptReviewDto(
    bool Shown,
    string CandidateName,
    decimal Score,
    decimal TotalPoints,
    decimal ScorePercentage,
    decimal PassMarkPercentage,
    bool Passed,
    IReadOnlyList<AttemptReviewQuestion> Questions);

public record AttemptReviewQuestion(
    Guid AttemptQuestionId,
    int DisplayOrder,
    QuestionType Type,
    string Text,
    string? ImageUrl,
    string? CorrectAnswerText,
    string? CandidateAnswerText,
    Guid? SelectedOptionId,
    bool WasAnswered,
    bool IsCorrect,
    IReadOnlyList<AttemptReviewOption> Options);

public record AttemptReviewOption(
    Guid Id,
    string Text,
    bool IsCorrect,
    bool WasSelected,
    int DisplayOrder);
