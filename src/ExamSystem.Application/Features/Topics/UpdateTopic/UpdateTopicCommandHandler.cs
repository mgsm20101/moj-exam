namespace ExamSystem.Application.Features.Topics.UpdateTopic;

public class UpdateTopicCommandHandler : IRequestHandler<UpdateTopicCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public UpdateTopicCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(UpdateTopicCommand request, CancellationToken cancellationToken)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (topic is null)
        {
            return Result<Unit>.Failure("Topic not found.");
        }

        var duplicateName = await _db.Topics.AnyAsync(t => t.Id != request.Id && t.Name == request.Name, cancellationToken);
        if (duplicateName)
        {
            return Result<Unit>.Failure("Topic name already exists.");
        }

        topic.Name = request.Name;
        topic.DisplayOrder = request.DisplayOrder;
        topic.IsActive = request.IsActive;
        topic.ModifiedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
