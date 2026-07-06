using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public class SubmitAttemptCommandHandler : IRequestHandler<SubmitAttemptCommand, Result<ResultDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IAttemptGradingService _grading;

    public SubmitAttemptCommandHandler(IApplicationDbContext db, IAttemptGradingService grading)
    {
        _db = db;
        _grading = grading;
    }

    public async Task<Result<ResultDto>> Handle(SubmitAttemptCommand request, CancellationToken cancellationToken)
    {
        var attempt = await _db.ExamAttempts
            .Include(a => a.Questions).ThenInclude(q => q.Options)
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null)
        {
            return Result<ResultDto>.Failure("Attempt not found.");
        }

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<ResultDto>.Failure("Exam not found.");
        }

        if (attempt.Status == ExamAttemptStatus.InProgress)
        {
            var expired = DateTime.UtcNow > attempt.ExpiresAtUtc;
            var grade = _grading.Grade(attempt, exam);
            attempt.Score = grade.Score;
            attempt.Status = expired ? ExamAttemptStatus.AutoSubmitted : ExamAttemptStatus.Submitted;
            attempt.SubmittedAtUtc = expired ? attempt.ExpiresAtUtc : DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result<ResultDto>.Success(BuildResult(attempt, exam));
    }

    internal static ResultDto BuildResult(ExamAttempt attempt, Exam exam)
    {
        var total = attempt.Questions.Sum(q => q.Type switch
        {
            Domain.Questions.QuestionType.Mcq => exam.McqPoints,
            Domain.Questions.QuestionType.TrueFalse => exam.TrueFalsePoints,
            Domain.Questions.QuestionType.FillBlank => exam.FillBlankPoints,
            _ => 0m
        });
        var score = attempt.Score ?? 0m;
        var passed = total > 0m && score / total * 100m >= exam.PassMarkPercentage;
        return new ResultDto(exam.ShowResultImmediately, score, total, exam.PassMarkPercentage, passed);
    }
}
