using ExamSystem.Application.Features.Exams.ArchiveExam;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class ArchiveExamCommandHandlerTests
{
    [Fact]
    public async Task Handle_ClosedExam_ArchivesIt()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 60, Status = ExamStatus.Closed };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new ArchiveExamCommandHandler(db);

        var result = await handler.Handle(new ArchiveExamCommand(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExamStatus.Archived, db.Exams.Single().Status);
    }

    [Fact]
    public async Task Handle_DraftExam_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 60 };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new ArchiveExamCommandHandler(db);

        var result = await handler.Handle(new ArchiveExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only Closed exams can be archived.", result.Errors);
    }
}
