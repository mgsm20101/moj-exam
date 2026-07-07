using ExamSystem.Application.Features.Exams.SetQueueMode;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class SetExamQueueModeCommandHandlerTests
{
    private static Exam NewExam(ExamStatus status) => new()
    {
        Name = "E",
        StartAtUtc = DateTime.UtcNow.AddHours(-1),
        EndAtUtc = DateTime.UtcNow.AddDays(1),
        DurationMinutes = 60,
        Status = status
    };

    [Theory]
    [InlineData(ExamStatus.Draft)]
    [InlineData(ExamStatus.Published)]
    public async Task Handle_DraftOrPublished_SetsMode(ExamStatus status)
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(status);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await new SetExamQueueModeCommandHandler(db)
            .Handle(new SetExamQueueModeCommand(exam.Id, QueueMode.Manual), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(QueueMode.Manual, db.Exams.Single().QueueMode);
    }

    [Theory]
    [InlineData(ExamStatus.Closed)]
    [InlineData(ExamStatus.Archived)]
    public async Task Handle_ClosedOrArchived_Fails(ExamStatus status)
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(status);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await new SetExamQueueModeCommandHandler(db)
            .Handle(new SetExamQueueModeCommand(exam.Id, QueueMode.Manual), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(QueueMode.Auto, db.Exams.Single().QueueMode);
    }

    [Fact]
    public async Task Handle_SameMode_IsIdempotentSuccess()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(ExamStatus.Published);
        exam.QueueMode = QueueMode.Manual;
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await new SetExamQueueModeCommandHandler(db)
            .Handle(new SetExamQueueModeCommand(exam.Id, QueueMode.Manual), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_UnknownExam_Fails()
    {
        using var db = TestDbContextFactory.Create();

        var result = await new SetExamQueueModeCommandHandler(db)
            .Handle(new SetExamQueueModeCommand(Guid.NewGuid(), QueueMode.Manual), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
