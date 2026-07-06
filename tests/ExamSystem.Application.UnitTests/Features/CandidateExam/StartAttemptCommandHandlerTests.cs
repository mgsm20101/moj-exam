using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.CandidateExam.StartAttempt;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Persistence;
using ExamSystem.Infrastructure.Selection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class StartAttemptCommandHandlerTests
{
    // 1999-12-31, Cairo(01), Female
    private const string Nid = "29912310123404";

    private sealed class FakeTokenGenerator : IAttemptTokenGenerator
    {
        public string GenerateToken(Guid attemptId, Guid candidateId, Guid examId, DateTime expiresAtUtc)
            => $"token-{attemptId}";
    }

    private sealed class FakeReconciler : IQueueReconciler
    {
        // No queue in these tests: capacity is always available.
        public Task<Common.Models.QueueCapacity> ReconcileAsync(Guid examId, CancellationToken ct)
            => Task.FromResult(new Common.Models.QueueCapacity(20, 0, 0));
    }

    private static Question Mcq(Guid topicId) => new()
    {
        TopicId = topicId, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium, Text = "Q", IsActive = true,
        Options = new List<QuestionOption> { new() { Text = "A", IsCorrect = true, DisplayOrder = 1 } }
    };

    private static async Task<(ApplicationDbContext db, Exam exam)> SeedAsync()
    {
        var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "T", DisplayOrder = 1 };
        db.Topics.Add(topic);
        for (var i = 0; i < 3; i++) db.Questions.Add(Mcq(topic.Id));
        var exam = new Exam
        {
            Name = "E", DurationMinutes = 60, Status = ExamStatus.Published,
            StartAtUtc = DateTime.UtcNow.AddHours(-1), EndAtUtc = DateTime.UtcNow.AddHours(1)
        };
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 2 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, exam);
    }

    [Fact]
    public async Task Handle_NewCandidate_CreatesAttemptWithSnapshotTimerAndToken()
    {
        var (db, exam) = await SeedAsync();
        var handler = new StartAttemptCommandHandler(db, new QuestionSelectionService(db), new FakeTokenGenerator(), new FakeReconciler());

        var result = await handler.Handle(
            new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var attempt = db.ExamAttempts.Include(a => a.Questions).Single();
        Assert.Equal(ExamAttemptStatus.InProgress, attempt.Status);
        Assert.Equal(2, attempt.Questions.Count);
        Assert.Equal(attempt.StartedAtUtc.AddMinutes(60), attempt.ExpiresAtUtc);
        Assert.Equal($"token-{attempt.Id}", result.Value!.AttemptToken);
        Assert.Equal("Started", result.Value!.Outcome);
    }

    [Fact]
    public async Task Handle_ExistingInProgressAttempt_IsIdempotent()
    {
        var (db, exam) = await SeedAsync();
        var handler = new StartAttemptCommandHandler(db, new QuestionSelectionService(db), new FakeTokenGenerator(), new FakeReconciler());
        var cmd = new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678");

        var first = await handler.Handle(cmd, CancellationToken.None);
        var second = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(first.Value!.AttemptId, second.Value!.AttemptId);
        Assert.Single(db.ExamAttempts);
    }

    [Fact]
    public async Task Handle_AlreadyTakenNoGrant_Fails()
    {
        var (db, exam) = await SeedAsync();
        var handler = new StartAttemptCommandHandler(db, new QuestionSelectionService(db), new FakeTokenGenerator(), new FakeReconciler());
        var cmd = new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678");

        var first = await handler.Handle(cmd, CancellationToken.None);
        // mark it submitted so it is no longer "in progress"
        var attempt = db.ExamAttempts.Single();
        attempt.Status = ExamAttemptStatus.Submitted;
        await db.SaveChangesAsync(CancellationToken.None);

        var second = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
    }
}
