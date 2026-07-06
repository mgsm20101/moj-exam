using ExamSystem.Application.Features.CandidateExam.GetExamLanding;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class GetExamLandingQueryHandlerTests
{
    [Fact]
    public async Task Handle_PublishedExamWithinWindow_ReturnsOpenWithQuestionCount()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "T", DisplayOrder = 1 };
        db.Topics.Add(topic);
        var exam = new Exam
        {
            Name = "Skills", DurationMinutes = 90, Status = ExamStatus.Published,
            StartAtUtc = DateTime.UtcNow.AddHours(-1), EndAtUtc = DateTime.UtcNow.AddHours(1)
        };
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 25 });
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Hard, Type = QuestionType.FillBlank, Count = 5 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamLandingQueryHandler(db);
        var result = await handler.Handle(new GetExamLandingQuery(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsOpen);
        Assert.Equal(30, result.Value.TotalQuestionCount);
        Assert.Equal(90, result.Value.DurationMinutes);
    }

    [Fact]
    public async Task Handle_DraftExam_IsNotOpen()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam
        {
            Name = "Draft", DurationMinutes = 60, Status = ExamStatus.Draft,
            StartAtUtc = DateTime.UtcNow.AddHours(-1), EndAtUtc = DateTime.UtcNow.AddHours(1)
        };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamLandingQueryHandler(db);
        var result = await handler.Handle(new GetExamLandingQuery(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsOpen);
    }

    [Fact]
    public async Task Handle_MissingExam_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var handler = new GetExamLandingQueryHandler(db);

        var result = await handler.Handle(new GetExamLandingQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
