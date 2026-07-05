using ExamSystem.Application.Features.Exams.PublishExam;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Persistence;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class PublishExamCommandHandlerTests
{
    private static async Task<(ApplicationDbContext Db, Exam Exam)> SeedDraftExamAsync(int mcqNeeded, int mcqAvailable)
    {
        var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);

        for (var i = 0; i < mcqAvailable; i++)
        {
            var question = new Question { TopicId = topic.Id, Text = $"Q{i}", Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium, IsActive = true };
            question.Options.Add(new QuestionOption { Text = "A", IsCorrect = false });
            question.Options.Add(new QuestionOption { Text = "B", IsCorrect = true });
            db.Questions.Add(question);
        }

        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow.AddHours(1), EndAtUtc = DateTime.UtcNow.AddDays(7), DurationMinutes = 60 };
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = mcqNeeded });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        return (db, exam);
    }

    [Fact]
    public async Task Handle_SufficientQuestionBank_PublishesExam()
    {
        var (db, exam) = await SeedDraftExamAsync(mcqNeeded: 5, mcqAvailable: 5);
        var handler = new PublishExamCommandHandler(db);

        var result = await handler.Handle(new PublishExamCommand(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExamStatus.Published, db.Exams.Single().Status);
    }

    [Fact]
    public async Task Handle_InsufficientQuestionBank_ReturnsFailureAndStaysDraft()
    {
        var (db, exam) = await SeedDraftExamAsync(mcqNeeded: 10, mcqAvailable: 3);
        var handler = new PublishExamCommandHandler(db);

        var result = await handler.Handle(new PublishExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("only 3 are available"));
        Assert.Equal(ExamStatus.Draft, db.Exams.Single().Status);
    }

    [Fact]
    public async Task Handle_AlreadyPublishedExam_ReturnsFailure()
    {
        var (db, exam) = await SeedDraftExamAsync(mcqNeeded: 1, mcqAvailable: 1);
        exam.Status = ExamStatus.Published;
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new PublishExamCommandHandler(db);

        var result = await handler.Handle(new PublishExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only Draft exams can be published.", result.Errors);
    }

    [Fact]
    public async Task Handle_EndDateInThePast_ReturnsFailure()
    {
        var (db, exam) = await SeedDraftExamAsync(mcqNeeded: 1, mcqAvailable: 1);
        exam.EndAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new PublishExamCommandHandler(db);

        var result = await handler.Handle(new PublishExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("End date must be in the future.", result.Errors);
    }

    [Fact]
    public async Task Handle_ExpiredEndDateAndInsufficientBank_ReturnsBothErrorsAccumulated()
    {
        var (db, exam) = await SeedDraftExamAsync(mcqNeeded: 10, mcqAvailable: 3);
        // Keep StartAtUtc < EndAtUtc (both in the past) so only the "end date must be in the
        // future" rule trips here, isolating this test to exactly the two errors under test.
        exam.StartAtUtc = DateTime.UtcNow.AddDays(-2);
        exam.EndAtUtc = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new PublishExamCommandHandler(db);

        var result = await handler.Handle(new PublishExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("End date must be in the future."));
        Assert.Contains(result.Errors, e => e.Contains("only 3 are available"));
        Assert.Equal(2, result.Errors.Count);
    }
}
