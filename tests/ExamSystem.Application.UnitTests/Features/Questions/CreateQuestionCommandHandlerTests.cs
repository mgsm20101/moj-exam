using ExamSystem.Application.Features.Questions.CreateQuestion;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Application.UnitTests.Features.Questions;

public class CreateQuestionCommandHandlerTests
{
    [Fact]
    public async Task Handle_McqQuestion_PersistsQuestionAndOptions()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateQuestionCommandHandler(db);
        var options = new List<QuestionOptionInput> { new("A", false), new("B", true) };
        var command = new CreateQuestionCommand(topic.Id, QuestionType.Mcq, DifficultyLevel.Medium, "Pick one", null, options, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = db.Questions.Include(q => q.Options).Single();
        Assert.Equal(2, saved.Options.Count);
        Assert.Single(saved.Options, o => o.IsCorrect);
    }

    [Fact]
    public async Task Handle_FillBlankQuestion_PersistsWithNoOptions()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateQuestionCommandHandler(db);
        var command = new CreateQuestionCommand(topic.Id, QuestionType.FillBlank, DifficultyLevel.Hard, "Fill ___", null, null, "server", null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = db.Questions.Include(q => q.Options).Single();
        Assert.Empty(saved.Options);
        Assert.Equal("server", saved.CorrectAnswerText);
    }
}
