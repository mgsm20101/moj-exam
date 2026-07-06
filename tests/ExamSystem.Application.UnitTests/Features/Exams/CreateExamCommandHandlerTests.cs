using ExamSystem.Application.Features.Exams;
using ExamSystem.Application.Features.Exams.CreateExam;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class CreateExamCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidExam_PersistsExamAndTopicSelectionsAsDraft()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateExamCommandHandler(db);
        var selections = new List<ExamTopicSelectionInput>
        {
            new(topic.Id, 1, DifficultyLevel.Medium, QuestionType.Mcq, 25),
            new(topic.Id, 1, DifficultyLevel.Hard, QuestionType.FillBlank, 5)
        };
        var command = new CreateExamCommand(
            "Excel Basics", null, DateTime.UtcNow, DateTime.UtcNow.AddDays(7), 60,
            2m, 1m, 5m, 60m, 1, true, true, true, 20, 3, selections);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = db.Exams.Include(e => e.TopicSelections).Single();
        Assert.Equal(2, saved.TopicSelections.Count);
        Assert.Equal(ExamStatus.Draft, saved.Status);
        Assert.Equal(result.Value, saved.Id);
    }
}
