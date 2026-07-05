using ExamSystem.Application.Features.Topics.CreateTopic;

namespace ExamSystem.Application.UnitTests.Features.Topics;

public class CreateTopicCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidTopic_PersistsAndReturnsId()
    {
        using var db = TestDbContextFactory.Create();
        var handler = new CreateTopicCommandHandler(db);

        var result = await handler.Handle(new CreateTopicCommand("Excel Skills", 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(db.Topics);
        Assert.Equal(result.Value, db.Topics.Single().Id);
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        db.Topics.Add(new ExamSystem.Domain.Topics.Topic { Name = "Excel Skills", DisplayOrder = 1 });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateTopicCommandHandler(db);
        var result = await handler.Handle(new CreateTopicCommand("Excel Skills", 2), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Topic name already exists.", result.Errors);
    }
}
