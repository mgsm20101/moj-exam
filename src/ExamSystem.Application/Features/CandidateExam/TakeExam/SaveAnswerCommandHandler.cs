using ExamSystem.Domain.Attempts;

namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public class SaveAnswerCommandHandler : IRequestHandler<SaveAnswerCommand, Result<bool>>
{
    private readonly IApplicationDbContext _db;
    private readonly IAttemptGradingService _grading;

    public SaveAnswerCommandHandler(IApplicationDbContext db, IAttemptGradingService grading)
    {
        _db = db;
        _grading = grading;
    }

    public async Task<Result<bool>> Handle(SaveAnswerCommand request, CancellationToken cancellationToken)
    {
        var attempt = await _db.ExamAttempts
            .Include(a => a.Questions).ThenInclude(q => q.Options)
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null)
        {
            return Result<bool>.Failure("Attempt not found.");
        }

        if (attempt.Status != ExamAttemptStatus.InProgress)
        {
            return Result<bool>.Failure("This attempt is closed.");
        }

        if (DateTime.UtcNow > attempt.ExpiresAtUtc)
        {
            var exam = await _db.Exams.FirstAsync(e => e.Id == attempt.ExamId, cancellationToken);
            var grade = _grading.Grade(attempt, exam);
            attempt.Score = grade.Score;
            attempt.Status = ExamAttemptStatus.AutoSubmitted;
            attempt.SubmittedAtUtc = attempt.ExpiresAtUtc;
            await _db.SaveChangesAsync(cancellationToken);
            return Result<bool>.Failure("Time is up; the exam was submitted automatically.");
        }

        var question = attempt.Questions.FirstOrDefault(q => q.Id == request.AttemptQuestionId);
        if (question is null)
        {
            return Result<bool>.Failure("Question is not part of this attempt.");
        }

        if (request.SelectedOptionId is { } optionId && question.Options.All(o => o.Id != optionId))
        {
            return Result<bool>.Failure("Selected option is not valid for this question.");
        }

        if (request.AnswerText is { Length: > 50 })
        {
            return Result<bool>.Failure("Answer is too long.");
        }

        var answer = attempt.Answers.FirstOrDefault(a => a.AttemptQuestionId == request.AttemptQuestionId);
        if (answer is null)
        {
            answer = new AttemptAnswer { AttemptId = attempt.Id, AttemptQuestionId = request.AttemptQuestionId };
            _db.AttemptAnswers.Add(answer);
        }
        answer.SelectedOptionId = request.SelectedOptionId;
        answer.AnswerText = request.AnswerText;
        answer.IsFlagged = request.IsFlagged;
        answer.AnsweredAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
