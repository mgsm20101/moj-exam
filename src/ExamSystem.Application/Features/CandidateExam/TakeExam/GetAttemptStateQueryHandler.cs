using ExamSystem.Domain.Attempts;

namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public class GetAttemptStateQueryHandler : IRequestHandler<GetAttemptStateQuery, Result<AttemptStateDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IAttemptGradingService _grading;

    public GetAttemptStateQueryHandler(IApplicationDbContext db, IAttemptGradingService grading)
    {
        _db = db;
        _grading = grading;
    }

    public async Task<Result<AttemptStateDto>> Handle(GetAttemptStateQuery request, CancellationToken cancellationToken)
    {
        var attempt = await _db.ExamAttempts
            .Include(a => a.Questions).ThenInclude(q => q.Options)
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null)
        {
            return Result<AttemptStateDto>.Failure("Attempt not found.");
        }

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<AttemptStateDto>.Failure("Exam not found.");
        }

        var now = DateTime.UtcNow;
        if (attempt.Status == ExamAttemptStatus.InProgress && now > attempt.ExpiresAtUtc)
        {
            var grade = _grading.Grade(attempt, exam);
            attempt.Score = grade.Score;
            attempt.Status = ExamAttemptStatus.AutoSubmitted;
            attempt.SubmittedAtUtc = attempt.ExpiresAtUtc;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var remaining = attempt.Status == ExamAttemptStatus.InProgress
            ? Math.Max(0, (int)(attempt.ExpiresAtUtc - now).TotalSeconds)
            : 0;

        var answersByQuestion = attempt.Answers.ToDictionary(a => a.AttemptQuestionId);
        var questions = attempt.Questions
            .OrderBy(q => q.DisplayOrder)
            .Select(q =>
            {
                answersByQuestion.TryGetValue(q.Id, out var answer);
                return new AttemptQuestionStateDto(
                    q.Id, q.DisplayOrder, q.Type.ToString(), q.TextSnapshot, q.ImageUrlSnapshot,
                    q.Options.OrderBy(o => o.DisplayOrder)
                        .Select(o => new AttemptOptionDto(o.Id, o.TextSnapshot)).ToList(),
                    answer?.SelectedOptionId, answer?.AnswerText, answer?.IsFlagged ?? false);
            })
            .ToList();

        return Result<AttemptStateDto>.Success(new AttemptStateDto(
            attempt.Status.ToString(), remaining, exam.ShowResultImmediately, questions));
    }
}
