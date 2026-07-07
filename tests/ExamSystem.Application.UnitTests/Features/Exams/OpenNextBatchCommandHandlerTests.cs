using ExamSystem.Application.Features.Exams.OpenNextBatch;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;
using ExamSystem.Infrastructure.Queue;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class OpenNextBatchCommandHandlerTests
{
    private static Exam NewExam(ExamStatus status, QueueMode mode, int max = 10) => new()
    {
        Name = "E",
        StartAtUtc = DateTime.UtcNow.AddHours(-1),
        EndAtUtc = DateTime.UtcNow.AddDays(1),
        DurationMinutes = 60,
        MaxConcurrentAttempts = max,
        GraceWindowMinutes = 3,
        Status = status,
        QueueMode = mode
    };

    private static WaitingQueueEntry Waiting(Guid examId, int minutesAgo) => new()
    {
        ExamId = examId, CandidateId = Guid.NewGuid(),
        EnqueuedAtUtc = DateTime.UtcNow.AddMinutes(-minutesAgo),
        Status = WaitingQueueStatus.Waiting
    };

    [Fact]
    public async Task Handle_ManualPublished_CallsBatchAndReportsNumbers()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(ExamStatus.Published, QueueMode.Manual, max: 10);
        db.Exams.Add(exam);
        db.WaitingQueueEntries.AddRange(Waiting(exam.Id, 9), Waiting(exam.Id, 6), Waiting(exam.Id, 3));
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new OpenNextBatchCommandHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new OpenNextBatchCommand(exam.Id, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.CalledCount);
        Assert.Equal(1, result.Value.RemainingWaiting);
        Assert.Equal(8, result.Value.AvailableAfter); // 10 - 0 active - 2 reserved
    }

    [Fact]
    public async Task Handle_EmptyQueue_SucceedsWithZeroCalled()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(ExamStatus.Published, QueueMode.Manual);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new OpenNextBatchCommandHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new OpenNextBatchCommand(exam.Id, 5), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.CalledCount);
    }

    [Fact]
    public async Task Handle_AutoModeExam_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(ExamStatus.Published, QueueMode.Auto);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new OpenNextBatchCommandHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new OpenNextBatchCommand(exam.Id, 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("manual", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ExamStatus.Draft)]
    [InlineData(ExamStatus.Closed)]
    [InlineData(ExamStatus.Archived)]
    public async Task Handle_NotPublished_Fails(ExamStatus status)
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(status, QueueMode.Manual);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new OpenNextBatchCommandHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new OpenNextBatchCommand(exam.Id, 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_UnknownExam_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var handler = new OpenNextBatchCommandHandler(db, new QueueReconciler(db));

        var result = await handler.Handle(new OpenNextBatchCommand(Guid.NewGuid(), 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
