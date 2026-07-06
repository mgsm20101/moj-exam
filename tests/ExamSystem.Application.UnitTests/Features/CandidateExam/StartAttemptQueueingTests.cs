using ExamSystem.Application.Features.CandidateExam.StartAttempt;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Queue;
using ExamSystem.Infrastructure.Selection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class StartAttemptQueueingTests
{
    private const string Nid = "29912310123404";
    private const string Nid2 = "30106152112354"; // valid male id, different candidate

    private sealed class FakeTokenGenerator : ExamSystem.Application.Common.Interfaces.IAttemptTokenGenerator
    {
        public string GenerateToken(Guid a, Guid c, Guid e, DateTime x) => $"token-{a}";
    }

    private static async Task<(Infrastructure.Persistence.ApplicationDbContext db, Exam exam)> SeedAsync(int max)
    {
        var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "T", DisplayOrder = 1 };
        db.Topics.Add(topic);
        for (var i = 0; i < 3; i++)
        {
            db.Questions.Add(new Question
            {
                TopicId = topic.Id, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium,
                Text = "Q", IsActive = true,
                Options = new List<QuestionOption> { new() { Text = "A", IsCorrect = true, DisplayOrder = 1 } }
            });
        }
        var exam = new Exam
        {
            Name = "E", DurationMinutes = 60, Status = ExamStatus.Published, MaxConcurrentAttempts = max,
            StartAtUtc = DateTime.UtcNow.AddHours(-1), EndAtUtc = DateTime.UtcNow.AddHours(1)
        };
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 1 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, exam);
    }

    private static StartAttemptCommandHandler Handler(Infrastructure.Persistence.ApplicationDbContext db) =>
        new(db, new QuestionSelectionService(db), new FakeTokenGenerator(), new QueueReconciler(db));

    [Fact]
    public async Task Start_AtCapacity_QueuesSecondCandidate()
    {
        var (db, exam) = await SeedAsync(max: 1);

        var first = await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);
        var second = await Handler(db).Handle(new StartAttemptCommand(exam.Id, "منى سمير علي حسن", Nid2, "01112345678"), CancellationToken.None);

        Assert.Equal("Started", first.Value!.Outcome);
        Assert.Equal("Queued", second.Value!.Outcome);
        Assert.Equal(1, second.Value.QueuePosition);
        Assert.Single(db.WaitingQueueEntries.Where(e => e.Status == WaitingQueueStatus.Waiting));
    }

    [Fact]
    public async Task Start_AfterSlotFrees_StartsQueuedCandidate()
    {
        var (db, exam) = await SeedAsync(max: 1);
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "منى سمير علي حسن", Nid2, "01112345678"), CancellationToken.None);

        // first candidate submits -> their attempt is no longer InProgress
        var attempt = db.ExamAttempts.Single();
        attempt.Status = ExamAttemptStatus.Submitted;
        await db.SaveChangesAsync(CancellationToken.None);

        // queued candidate presses start again -> now gets an attempt
        var retry = await Handler(db).Handle(new StartAttemptCommand(exam.Id, "منى سمير علي حسن", Nid2, "01112345678"), CancellationToken.None);

        Assert.Equal("Started", retry.Value!.Outcome);
        Assert.False(string.IsNullOrEmpty(retry.Value.AttemptToken));
    }

    [Fact]
    public async Task Start_RepeatedWhileQueued_IsIdempotent()
    {
        var (db, exam) = await SeedAsync(max: 1);
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "منى سمير علي حسن", Nid2, "01112345678"), CancellationToken.None);
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "منى سمير علي حسن", Nid2, "01112345678"), CancellationToken.None);

        Assert.Single(db.WaitingQueueEntries); // no duplicate queue entry
    }

    [Fact]
    public async Task Start_ManualMode_WithFreeCapacity_StillQueues()
    {
        var (db, exam) = await SeedAsync(max: 20);
        exam.QueueMode = QueueMode.Manual;
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.Equal("Queued", result.Value!.Outcome);
        Assert.Equal(1, result.Value.QueuePosition);
        Assert.Empty(db.ExamAttempts);
    }

    [Fact]
    public async Task Start_ManualMode_CalledCandidate_Starts()
    {
        var (db, exam) = await SeedAsync(max: 20);
        exam.QueueMode = QueueMode.Manual;
        await db.SaveChangesAsync(CancellationToken.None);

        // enqueue, then the admin opens a batch of 1
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);
        await new QueueReconciler(db).CallNextBatchAsync(exam.Id, 1, CancellationToken.None);

        var retry = await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.Equal("Started", retry.Value!.Outcome);
        Assert.Equal(WaitingQueueStatus.Started, db.WaitingQueueEntries.Single().Status);
    }
}
