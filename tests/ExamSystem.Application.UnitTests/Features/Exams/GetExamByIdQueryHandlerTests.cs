using ExamSystem.Application.Features.Exams.GetExamById;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class GetExamByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingExam_ReturnsDetailWithTopicNames()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(7), DurationMinutes = 60 };
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 25 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamByIdQueryHandler(db);
        var result = await handler.Handle(new GetExamByIdQuery(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Excel", result.Value!.TopicSelections.Single().TopicName);
    }

    [Fact]
    public async Task Handle_UnknownExam_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        var handler = new GetExamByIdQueryHandler(db);

        var result = await handler.Handle(new GetExamByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
