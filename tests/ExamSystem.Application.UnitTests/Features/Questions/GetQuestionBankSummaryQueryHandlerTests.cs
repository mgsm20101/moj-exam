using ExamSystem.Application.Features.Questions.GetQuestionBankSummary;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Questions;

public class GetQuestionBankSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_GroupsActiveQuestionsByTopicAndDifficulty()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        db.Questions.AddRange(
            new Question { TopicId = topic.Id, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium, Text = "q1", IsActive = true },
            new Question { TopicId = topic.Id, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium, Text = "q2", IsActive = true },
            new Question { TopicId = topic.Id, Type = QuestionType.FillBlank, Difficulty = DifficultyLevel.Medium, Text = "q3", CorrectAnswerText = "a", IsActive = true },
            new Question { TopicId = topic.Id, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Hard, Text = "q4", IsActive = false });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetQuestionBankSummaryQueryHandler(db);
        var result = await handler.Handle(new GetQuestionBankSummaryQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value!, r => r.TopicName == "Excel" && r.Difficulty == DifficultyLevel.Medium);
        Assert.Equal(2, row.McqCount);
        Assert.Equal(1, row.FillBlankCount);
        Assert.DoesNotContain(result.Value!, r => r.Difficulty == DifficultyLevel.Hard);
    }
}
