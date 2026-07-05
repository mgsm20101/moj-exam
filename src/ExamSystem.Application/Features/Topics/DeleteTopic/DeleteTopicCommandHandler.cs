namespace ExamSystem.Application.Features.Topics.DeleteTopic;

public class DeleteTopicCommandHandler : IRequestHandler<DeleteTopicCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public DeleteTopicCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(DeleteTopicCommand request, CancellationToken cancellationToken)
    {
        var topic = await _db.Topics
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (topic is null)
        {
            return Result<Unit>.Failure("Topic not found.");
        }

        if (topic.Questions.Count > 0)
        {
            return Result<Unit>.Failure("Cannot delete a topic that has questions -- deactivate it instead.");
        }

        _db.Topics.Remove(topic);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
