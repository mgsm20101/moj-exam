namespace ExamSystem.Application.Features.Topics.GetTopics;

public class GetTopicsQueryHandler : IRequestHandler<GetTopicsQuery, Result<List<TopicDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetTopicsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<TopicDto>>> Handle(GetTopicsQuery request, CancellationToken cancellationToken)
    {
        var topics = await _db.Topics
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new TopicDto(t.Id, t.Name, t.DisplayOrder, t.IsActive, t.Questions.Count))
            .ToListAsync(cancellationToken);

        return Result<List<TopicDto>>.Success(topics);
    }
}
