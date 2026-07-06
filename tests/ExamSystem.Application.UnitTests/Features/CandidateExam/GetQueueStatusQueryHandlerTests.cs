using ExamSystem.Application.Features.CandidateExam.Queue;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;
using ExamSystem.Infrastructure.Queue;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class GetQueueStatusQueryHandlerTests
{
    private const string Nid = "29912310123404";

    private static async Task<(Infrastructure.Persistence.ApplicationDbContext db, Exam exam, Candidate candidate)> SeedAsync(int max)
    {
        var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "E", DurationMinutes = 60, MaxConcurrentAttempts = max };
        var candidate = new Candidate { NationalId = Nid, FullName = "x", MobileNumber = "01012345678" };
        db.Exams.Add(exam);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, exam, candidate);
    }

    [Fact]
    public async Task Handle_WaitingCandidate_ReturnsPosition()
    {
        var (db, exam, candidate) = await SeedAsync(max: 0); // no capacity -> stays waiting
        db.WaitingQueueEntries.Add(new WaitingQueueEntry
        { ExamId = exam.Id, CandidateId = candidate.Id, EnqueuedAtUtc = DateTime.UtcNow.AddMinutes(-1), Status = WaitingQueueStatus.Waiting });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetQueueStatusQueryHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new GetQueueStatusQuery(exam.Id, Nid), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Waiting", result.Value!.Status);
        Assert.Equal(1, result.Value.Position);
    }

    [Fact]
    public async Task Handle_SlotAvailable_PromotesToCalled()
    {
        var (db, exam, candidate) = await SeedAsync(max: 1); // capacity -> reconcile promotes
        db.WaitingQueueEntries.Add(new WaitingQueueEntry
        { ExamId = exam.Id, CandidateId = candidate.Id, EnqueuedAtUtc = DateTime.UtcNow.AddMinutes(-1), Status = WaitingQueueStatus.Waiting });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetQueueStatusQueryHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new GetQueueStatusQuery(exam.Id, Nid), CancellationToken.None);

        Assert.Equal("Called", result.Value!.Status);
    }

    [Fact]
    public async Task Handle_UnknownCandidate_ReturnsNotQueued()
    {
        var (db, exam, _) = await SeedAsync(max: 5);

        var handler = new GetQueueStatusQueryHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new GetQueueStatusQuery(exam.Id, "30106152112354"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("NotQueued", result.Value!.Status);
    }
}
