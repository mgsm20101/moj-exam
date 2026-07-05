using ExamSystem.Application.Features.Topics.DeleteTopic;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Topics;

public class DeleteTopicCommandHandlerTests
{
    [Fact]
    public async Task Handle_TopicWithNoQuestions_DeletesIt()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Empty Topic", DisplayOrder = 1 };
        db.Topics.Add(topic);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteTopicCommandHandler(db);
        var result = await handler.Handle(new DeleteTopicCommand(topic.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(db.Topics);
    }

    [Fact]
    public async Task Handle_TopicWithQuestions_ReturnsFailureAndKeepsIt()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Has Questions", DisplayOrder = 1 };
        topic.Questions.Add(new Question { Text = "Q", Type = QuestionType.FillBlank, Difficulty = DifficultyLevel.Medium, CorrectAnswerText = "a" });
        db.Topics.Add(topic);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteTopicCommandHandler(db);
        var result = await handler.Handle(new DeleteTopicCommand(topic.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Cannot delete a topic that has questions -- deactivate it instead.", result.Errors);
        Assert.Single(db.Topics);
    }
}
