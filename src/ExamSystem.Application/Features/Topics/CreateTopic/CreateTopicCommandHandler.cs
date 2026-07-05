using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.Features.Topics.CreateTopic;

public class CreateTopicCommandHandler : IRequestHandler<CreateTopicCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;

    public CreateTopicCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateTopicCommand request, CancellationToken cancellationToken)
    {
        var nameExists = await _db.Topics.AnyAsync(t => t.Name == request.Name, cancellationToken);
        if (nameExists)
        {
            return Result<Guid>.Failure("Topic name already exists.");
        }

        var topic = new Topic { Name = request.Name, DisplayOrder = request.DisplayOrder };
        _db.Topics.Add(topic);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(topic.Id);
    }
}
