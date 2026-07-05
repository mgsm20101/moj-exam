namespace ExamSystem.Application.Features.Questions.GetQuestions;

public class GetQuestionsQueryHandler : IRequestHandler<GetQuestionsQuery, Result<List<QuestionDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetQuestionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<QuestionDto>>> Handle(GetQuestionsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Questions.Include(q => q.Topic).Include(q => q.Options).AsQueryable();

        if (request.TopicId is not null)
        {
            query = query.Where(q => q.TopicId == request.TopicId);
        }
        if (request.Difficulty is not null)
        {
            query = query.Where(q => q.Difficulty == request.Difficulty);
        }
        if (request.IsActive is not null)
        {
            query = query.Where(q => q.IsActive == request.IsActive);
        }

        var questions = await query
            .OrderBy(q => q.Topic!.DisplayOrder).ThenBy(q => q.Difficulty)
            .Select(q => new QuestionDto(
                q.Id, q.TopicId, q.Topic!.Name, q.Type, q.Difficulty, q.Text, q.ImageUrl,
                q.CorrectAnswerText, q.PointsOverride, q.IsActive,
                q.Options.OrderBy(o => o.DisplayOrder)
                    .Select(o => new QuestionOptionDto(o.Id, o.Text, o.IsCorrect, o.DisplayOrder))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Result<List<QuestionDto>>.Success(questions);
    }
}
