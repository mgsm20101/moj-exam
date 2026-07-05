using ExamSystem.Application.Features.Exams;
using ExamSystem.Application.Features.Exams.UpdateExam;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class UpdateExamCommandHandlerTests
{
    private static async Task<(ApplicationDbContext Db, Exam Exam, Guid TopicId)> SeedDraftExamAsync()
    {
        var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        var exam = new Exam { Name = "Original", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(7), DurationMinutes = 30 };
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 5 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, exam, topic.Id);
    }

    [Fact]
    public async Task Handle_DraftExam_UpdatesFieldsAndReplacesTopicSelections()
    {
        var (db, exam, topicId) = await SeedDraftExamAsync();
        var handler = new UpdateExamCommandHandler(db);
        var newSelections = new List<ExamTopicSelectionInput> { new(topicId, 1, DifficultyLevel.Hard, QuestionType.FillBlank, 3) };
        var command = new UpdateExamCommand(
            exam.Id, "Renamed", "desc", DateTime.UtcNow, DateTime.UtcNow.AddDays(14), 45,
            3m, 1m, 6m, 70m, 2, false, false, false, newSelections);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var updated = db.Exams.Include(e => e.TopicSelections).Single();
        Assert.Equal("Renamed", updated.Name);
        Assert.Single(updated.TopicSelections);
        Assert.Equal(DifficultyLevel.Hard, updated.TopicSelections.Single().Difficulty);
    }

    [Fact]
    public async Task Handle_PublishedExam_ReturnsFailureAndLeavesItUnchanged()
    {
        var (db, exam, topicId) = await SeedDraftExamAsync();
        exam.Status = ExamStatus.Published;
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateExamCommandHandler(db);
        var command = new UpdateExamCommand(
            exam.Id, "Renamed", null, DateTime.UtcNow, DateTime.UtcNow.AddDays(14), 45,
            2m, 1m, 5m, 60m, 1, true, true, true,
            new List<ExamTopicSelectionInput> { new(topicId, 1, DifficultyLevel.Medium, QuestionType.Mcq, 5) });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only Draft exams can be edited.", result.Errors);
        Assert.Equal("Original", db.Exams.Single().Name);
    }
}
