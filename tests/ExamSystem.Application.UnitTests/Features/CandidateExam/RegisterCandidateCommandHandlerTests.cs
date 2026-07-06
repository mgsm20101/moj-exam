using ExamSystem.Application.Features.CandidateExam.RegisterCandidate;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class RegisterCandidateCommandHandlerTests
{
    // 1999-12-31, Cairo(01), gender digit even -> Female
    private const string Nid = "29912310123404";

    private static Exam OpenExam() => new()
    {
        Name = "E", DurationMinutes = 60, Status = ExamStatus.Published,
        StartAtUtc = DateTime.UtcNow.AddHours(-1), EndAtUtc = DateTime.UtcNow.AddHours(1)
    };

    [Fact]
    public async Task Handle_NewCandidate_CreatesProfileAndReturnsCanStart()
    {
        using var db = TestDbContextFactory.Create();
        var exam = OpenExam();
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterCandidateCommandHandler(db);
        var result = await handler.Handle(
            new RegisterCandidateCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegisterOutcome.CanStart, result.Value!.Status);
        var candidate = Assert.Single(db.Candidates);
        Assert.Equal(Nid, candidate.NationalId);
        Assert.Equal(Gender.Female, candidate.Gender);
    }

    [Fact]
    public async Task Handle_ClosedExam_ReturnsNotOpen()
    {
        using var db = TestDbContextFactory.Create();
        var exam = OpenExam();
        exam.Status = ExamStatus.Draft;
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterCandidateCommandHandler(db);
        var result = await handler.Handle(
            new RegisterCandidateCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegisterOutcome.NotOpen, result.Value!.Status);
    }

    [Fact]
    public async Task Handle_AlreadyAttemptedNoGrant_ReturnsAlreadyTaken()
    {
        using var db = TestDbContextFactory.Create();
        var exam = OpenExam();
        var candidate = new Candidate { NationalId = Nid, FullName = "x", MobileNumber = "01012345678" };
        db.Exams.Add(exam);
        db.Candidates.Add(candidate);
        db.ExamAttempts.Add(new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = candidate.Id,
            StartedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(60)
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterCandidateCommandHandler(db);
        var result = await handler.Handle(
            new RegisterCandidateCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegisterOutcome.AlreadyTaken, result.Value!.Status);
    }

    [Fact]
    public async Task Handle_AlreadyAttemptedWithActiveGrant_ReturnsCanStart()
    {
        using var db = TestDbContextFactory.Create();
        var exam = OpenExam();
        var candidate = new Candidate { NationalId = Nid, FullName = "x", MobileNumber = "01012345678" };
        db.Exams.Add(exam);
        db.Candidates.Add(candidate);
        db.ExamAttempts.Add(new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = candidate.Id,
            StartedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(60),
            Status = ExamAttemptStatus.Submitted
        });
        db.CandidateExamAttemptGrants.Add(new CandidateExamAttemptGrant
        { CandidateId = candidate.Id, ExamId = exam.Id, IsActive = true });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterCandidateCommandHandler(db);
        var result = await handler.Handle(
            new RegisterCandidateCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegisterOutcome.CanStart, result.Value!.Status);
    }
}
