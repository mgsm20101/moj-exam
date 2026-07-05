using ExamSystem.Application.Features.Exams.CloseExam;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class CloseExamCommandHandlerTests
{
    [Fact]
    public async Task Handle_PublishedExam_ClosesIt()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 60, Status = ExamStatus.Published };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new CloseExamCommandHandler(db);

        var result = await handler.Handle(new CloseExamCommand(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExamStatus.Closed, db.Exams.Single().Status);
    }

    [Fact]
    public async Task Handle_DraftExam_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 60 };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new CloseExamCommandHandler(db);

        var result = await handler.Handle(new CloseExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only Published exams can be closed.", result.Errors);
    }
}
