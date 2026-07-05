using ExamSystem.Application.Features.Exams.GetExams;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class GetExamsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTotalQuestionCountAndTotalPointsPerExam()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(7), DurationMinutes = 60, McqPoints = 2m, FillBlankPoints = 5m };
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 25 });
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Hard, Type = QuestionType.FillBlank, Count = 5 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamsQueryHandler(db);
        var result = await handler.Handle(new GetExamsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Value!);
        Assert.Equal(30, dto.TotalQuestionCount);
        Assert.Equal(75m, dto.TotalPoints);
    }
}
