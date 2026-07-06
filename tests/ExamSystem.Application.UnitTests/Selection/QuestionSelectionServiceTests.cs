using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Selection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamSystem.Application.UnitTests.Selection;

public class QuestionSelectionServiceTests
{
    private static Question Mcq(Guid topicId, string text) => new()
    {
        TopicId = topicId, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium,
        Text = text, IsActive = true,
        Options = new List<QuestionOption>
        {
            new() { Text = "A", IsCorrect = false, DisplayOrder = 1 },
            new() { Text = "B", IsCorrect = true, DisplayOrder = 2 }
        }
    };

    [Fact]
    public async Task BuildSnapshot_SelectsRequestedCount_DeterministicallyBySeed()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "T", DisplayOrder = 1 };
        db.Topics.Add(topic);
        for (var i = 0; i < 5; i++) db.Questions.Add(Mcq(topic.Id, $"Q{i}"));
        var exam = new Exam { Name = "E", DurationMinutes = 30 };
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 3 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var service = new QuestionSelectionService(db);
        var full = await db.Exams.Include(e => e.TopicSelections).SingleAsync();

        var a = await service.BuildSnapshotAsync(full, seed: 42, CancellationToken.None);
        var b = await service.BuildSnapshotAsync(full, seed: 42, CancellationToken.None);

        Assert.True(a.IsSuccess);
        Assert.Equal(3, a.Value!.Count);
        Assert.All(a.Value, q => Assert.Equal(2, q.Options.Count));
        Assert.Equal(1, a.Value[0].DisplayOrder);
        Assert.Equal(
            a.Value.Select(q => q.SourceQuestionId),
            b.Value!.Select(q => q.SourceQuestionId)); // same seed -> same selection
    }

    [Fact]
    public async Task BuildSnapshot_InsufficientPool_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "T", DisplayOrder = 1 };
        db.Topics.Add(topic);
        db.Questions.Add(Mcq(topic.Id, "only one"));
        var exam = new Exam { Name = "E", DurationMinutes = 30 };
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 3 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var service = new QuestionSelectionService(db);
        var full = await db.Exams.Include(e => e.TopicSelections).SingleAsync();

        var result = await service.BuildSnapshotAsync(full, seed: 1, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
