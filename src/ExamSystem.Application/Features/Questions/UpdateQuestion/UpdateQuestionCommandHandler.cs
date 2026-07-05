using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.UpdateQuestion;

public class UpdateQuestionCommandHandler : IRequestHandler<UpdateQuestionCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public UpdateQuestionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(UpdateQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _db.Questions
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        if (question is null)
        {
            return Result<Unit>.Failure("Question not found.");
        }

        question.TopicId = request.TopicId;
        question.Type = request.Type;
        question.Difficulty = request.Difficulty;
        question.Text = request.Text;
        question.ImageUrl = request.ImageUrl;
        question.CorrectAnswerText = request.CorrectAnswerText;
        question.PointsOverride = request.PointsOverride;
        question.IsActive = request.IsActive;
        question.ModifiedAtUtc = DateTime.UtcNow;

        question.Options.Clear();
        if (request.Options is not null)
        {
            for (var i = 0; i < request.Options.Count; i++)
            {
                question.Options.Add(new QuestionOption
                {
                    Text = request.Options[i].Text,
                    IsCorrect = request.Options[i].IsCorrect,
                    DisplayOrder = i + 1
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
