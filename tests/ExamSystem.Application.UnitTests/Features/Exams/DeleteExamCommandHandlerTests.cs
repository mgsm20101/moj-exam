using ExamSystem.Application.Features.Exams.DeleteExam;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class DeleteExamCommandHandlerTests
{
    [Fact]
    public async Task Handle_DraftExam_DeletesIt()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Draft Exam", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 30 };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteExamCommandHandler(db);
        var result = await handler.Handle(new DeleteExamCommand(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(db.Exams);
    }

    [Fact]
    public async Task Handle_PublishedExam_ReturnsFailureAndKeepsIt()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Published Exam", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 30, Status = ExamStatus.Published };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteExamCommandHandler(db);
        var result = await handler.Handle(new DeleteExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only Draft exams can be deleted -- archive it instead.", result.Errors);
        Assert.Single(db.Exams);
    }
}
