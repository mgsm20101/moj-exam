using ExamSystem.Application.Features.Exams.CloneExam;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class CloneExamCommandHandlerTests
{
    [Fact]
    public async Task Handle_PublishedExam_CreatesDraftCopyWithSameTopicSelections()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        var original = new Exam { Name = "Original Exam", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(7), DurationMinutes = 60, Status = ExamStatus.Published };
        original.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 25 });
        db.Exams.Add(original);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CloneExamCommandHandler(db);
        var result = await handler.Handle(new CloneExamCommand(original.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var clone = db.Exams.Include(e => e.TopicSelections).Single(e => e.Id == result.Value);
        Assert.Equal(ExamStatus.Draft, clone.Status);
        Assert.Equal("Original Exam (Copy)", clone.Name);
        Assert.Single(clone.TopicSelections);
        Assert.NotEqual(original.Id, clone.Id);
    }
}
