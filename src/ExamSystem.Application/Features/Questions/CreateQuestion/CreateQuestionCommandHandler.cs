using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.CreateQuestion;

public class CreateQuestionCommandHandler : IRequestHandler<CreateQuestionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;

    public CreateQuestionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = new Question
        {
            TopicId = request.TopicId,
            Type = request.Type,
            Difficulty = request.Difficulty,
            Text = request.Text,
            ImageUrl = request.ImageUrl,
            CorrectAnswerText = request.CorrectAnswerText,
            PointsOverride = request.PointsOverride
        };

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

        _db.Questions.Add(question);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(question.Id);
    }
}
