using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Reports.GetAttemptReview;

public class GetAttemptReviewQueryHandler : IRequestHandler<GetAttemptReviewQuery, Result<AttemptReviewDto>>
{
    private readonly IApplicationDbContext _db;

    public GetAttemptReviewQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<AttemptReviewDto>> Handle(GetAttemptReviewQuery request, CancellationToken cancellationToken)
    {
        var attempt = await _db.ExamAttempts
            .Include(a => a.Questions).ThenInclude(q => q.Options)
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null)
        {
            return Result<AttemptReviewDto>.Failure("Attempt not found.");
        }

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<AttemptReviewDto>.Failure("Exam not found.");
        }

        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.Id == attempt.CandidateId, cancellationToken);
        var candidateName = candidate?.FullName ?? string.Empty;

        var total = attempt.Questions.Sum(q => PointsFor(exam, q.Type));
        var score = attempt.Score ?? 0m;
        var percentage = total > 0m ? Math.Truncate(score / total * 10000m) / 100m : 0m;
        var passed = total > 0m && score / total * 100m >= exam.PassMarkPercentage;

        // Candidate flow honours the exam's ShowResultImmediately gate exactly like ResultDto; the
        // admin flow (EnforceShowResultGate == false) always sees the breakdown.
        if (request.EnforceShowResultGate && !exam.ShowResultImmediately)
        {
            return Result<AttemptReviewDto>.Success(new AttemptReviewDto(
                false, candidateName, 0m, 0m, 0m, exam.PassMarkPercentage, false,
                Array.Empty<AttemptReviewQuestion>()));
        }

        // Index answers by their snapshot question; an unanswered question simply has no entry and is
        // reported as not-answered / incorrect (grading treats missing answers as wrong).
        var answersByQuestion = attempt.Answers.ToDictionary(a => a.AttemptQuestionId);

        var questions = attempt.Questions
            .OrderBy(q => q.DisplayOrder)
            .Select(q =>
            {
                answersByQuestion.TryGetValue(q.Id, out var answer);

                var options = q.Options
                    .OrderBy(o => o.DisplayOrder)
                    .Select(o => new AttemptReviewOption(
                        o.Id,
                        o.TextSnapshot,
                        // Candidates never learn which option is correct — only whether their own
                        // choice was right (via the question-level IsCorrect verdict below).
                        request.RevealCorrectAnswers && o.IsCorrect,
                        answer?.SelectedOptionId == o.Id,
                        o.DisplayOrder))
                    .ToList();

                return new AttemptReviewQuestion(
                    q.Id,
                    q.DisplayOrder,
                    q.Type,
                    q.TextSnapshot,
                    q.ImageUrlSnapshot,
                    request.RevealCorrectAnswers ? q.CorrectAnswerTextSnapshot : null,
                    answer?.AnswerText,
                    answer?.SelectedOptionId,
                    answer is not null,
                    answer?.IsCorrect ?? false,
                    options);
            })
            .ToList();

        return Result<AttemptReviewDto>.Success(new AttemptReviewDto(
            true, candidateName, score, total, percentage, exam.PassMarkPercentage, passed, questions));
    }

    private static decimal PointsFor(Domain.Exams.Exam exam, QuestionType type) => type switch
    {
        QuestionType.Mcq => exam.McqPoints,
        QuestionType.TrueFalse => exam.TrueFalsePoints,
        QuestionType.FillBlank => exam.FillBlankPoints,
        _ => 0m
    };
}
