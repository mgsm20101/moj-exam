using ExamSystem.Application.Features.Reports.GetExamResultsReport;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Persistence;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.Reports;

public class GetExamResultsReportQueryHandlerTests
{
    /// <summary>Seeds one exam configured as 25 MCQ (2 pts) + 5 FillBlank (5 pts) = 75 marks, pass mark 60% (= 45).</summary>
    private static (ApplicationDbContext Db, Exam Exam) SeedExam(decimal passMarkPercentage = 60m)
    {
        var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1, IsActive = true };
        db.Topics.Add(topic);

        var exam = new Exam
        {
            Name = "Excel Basics",
            DurationMinutes = 60,
            McqPoints = 2m,
            TrueFalsePoints = 1m,
            FillBlankPoints = 5m,
            PassMarkPercentage = passMarkPercentage,
            Status = ExamStatus.Closed
        };
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 25 });
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Hard, Type = QuestionType.FillBlank, Count = 5 });
        db.Exams.Add(exam);
        return (db, exam);
    }

    private static Candidate AddCandidate(ApplicationDbContext db, string name, string nationalId)
    {
        var candidate = new Candidate
        {
            FullName = name,
            NationalId = nationalId,
            MobileNumber = "01000000000",
            BirthDateUtc = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            GovernorateCode = 1
        };
        db.Candidates.Add(candidate);
        return candidate;
    }

    private static ExamAttempt AddAttempt(ApplicationDbContext db, Exam exam, Candidate candidate, decimal score,
        ExamAttemptStatus status = ExamAttemptStatus.Submitted, DateTime? submittedAt = null, int tabSwitchCount = 0)
    {
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id,
            CandidateId = candidate.Id,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            SubmittedAtUtc = submittedAt ?? DateTime.UtcNow,
            Status = status,
            Score = score,
            TabSwitchCount = tabSwitchCount
        };
        db.ExamAttempts.Add(attempt);
        return attempt;
    }

    [Fact]
    public async Task Handle_ClassifiesPassFail_UsingTheGradingFormula()
    {
        var (db, exam) = SeedExam(passMarkPercentage: 60m);
        var pass = AddCandidate(db, "Passer", "29001010100011");
        var fail = AddCandidate(db, "Failer", "29001010100012");
        AddAttempt(db, exam, pass, score: 45m); // 45/75 = 60% -> exactly the pass mark -> passed
        AddAttempt(db, exam, fail, score: 44m); // 44/75 = 58.67% -> failed
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamResultsReportQueryHandler(db);
        var result = await handler.Handle(new GetExamResultsReportQuery(exam.Id, ResultsFilter.All), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var report = result.Value!;
        Assert.Equal(75m, report.TotalPoints);
        Assert.Equal(45m, report.PassMarkPoints);
        Assert.Equal(2, report.Summary.TotalCandidates);
        Assert.Equal(1, report.Summary.PassedCount);
        Assert.Equal(1, report.Summary.FailedCount);
        Assert.Equal(50m, report.Summary.PassRatePercentage);
        Assert.True(report.Rows.Single(r => r.NationalId == "29001010100011").Passed);
        Assert.False(report.Rows.Single(r => r.NationalId == "29001010100012").Passed);
    }

    [Fact]
    public async Task Handle_PassedFilter_RestrictsRowsButKeepsSummaryOverEveryone()
    {
        var (db, exam) = SeedExam();
        AddAttempt(db, exam, AddCandidate(db, "P1", "29001010100011"), score: 60m);
        AddAttempt(db, exam, AddCandidate(db, "P2", "29001010100012"), score: 50m);
        AddAttempt(db, exam, AddCandidate(db, "F1", "29001010100013"), score: 10m);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamResultsReportQueryHandler(db);
        var result = await handler.Handle(new GetExamResultsReportQuery(exam.Id, ResultsFilter.Passed), CancellationToken.None);

        var report = result.Value!;
        Assert.Equal(3, report.Summary.TotalCandidates); // summary unaffected by the filter
        Assert.Equal(2, report.Summary.PassedCount);
        Assert.Equal(1, report.Summary.FailedCount);
        Assert.Equal(2, report.Rows.Count);            // rows restricted to passers
        Assert.All(report.Rows, r => Assert.True(r.Passed));
    }

    [Fact]
    public async Task Handle_FailedFilter_RestrictsRowsToFailers()
    {
        var (db, exam) = SeedExam();
        AddAttempt(db, exam, AddCandidate(db, "P1", "29001010100011"), score: 60m);
        AddAttempt(db, exam, AddCandidate(db, "F1", "29001010100013"), score: 10m);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamResultsReportQueryHandler(db);
        var result = await handler.Handle(new GetExamResultsReportQuery(exam.Id, ResultsFilter.Failed), CancellationToken.None);

        var row = Assert.Single(result.Value!.Rows);
        Assert.False(row.Passed);
        Assert.Equal("F1", row.FullName);
    }

    [Fact]
    public async Task Handle_UsesBestAttemptPerCandidate()
    {
        var (db, exam) = SeedExam();
        var candidate = AddCandidate(db, "Retaker", "29001010100011");
        AddAttempt(db, exam, candidate, score: 30m, submittedAt: DateTime.UtcNow.AddMinutes(-20));
        AddAttempt(db, exam, candidate, score: 50m, submittedAt: DateTime.UtcNow.AddMinutes(-5));
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamResultsReportQueryHandler(db);
        var result = await handler.Handle(new GetExamResultsReportQuery(exam.Id), CancellationToken.None);

        var report = result.Value!;
        Assert.Equal(1, report.Summary.TotalCandidates);
        var row = Assert.Single(report.Rows);
        Assert.Equal(50m, row.Score); // best score wins
        Assert.True(row.Passed);
    }

    [Fact]
    public async Task Handle_ExcludesInProgressAndTerminatedAttempts()
    {
        var (db, exam) = SeedExam();
        AddAttempt(db, exam, AddCandidate(db, "InProgress", "29001010100011"), score: 70m, status: ExamAttemptStatus.InProgress);
        AddAttempt(db, exam, AddCandidate(db, "Terminated", "29001010100012"), score: 70m, status: ExamAttemptStatus.Terminated);
        AddAttempt(db, exam, AddCandidate(db, "AutoSubmitted", "29001010100013"), score: 46m, status: ExamAttemptStatus.AutoSubmitted);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamResultsReportQueryHandler(db);
        var result = await handler.Handle(new GetExamResultsReportQuery(exam.Id), CancellationToken.None);

        var report = result.Value!;
        Assert.Equal(1, report.Summary.TotalCandidates); // only the AutoSubmitted attempt counts
        Assert.Equal("AutoSubmitted", Assert.Single(report.Rows).FullName);
    }

    [Fact]
    public async Task Handle_TieOnScore_PicksTheLaterSubmittedAttempt()
    {
        var (db, exam) = SeedExam();
        var candidate = AddCandidate(db, "Retaker", "29001010100011");
        // Same best score, different submission times + a distinguishing TabSwitchCount.
        AddAttempt(db, exam, candidate, score: 50m, submittedAt: DateTime.UtcNow.AddMinutes(-20), tabSwitchCount: 9);
        AddAttempt(db, exam, candidate, score: 50m, submittedAt: DateTime.UtcNow.AddMinutes(-5), tabSwitchCount: 3);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamResultsReportQueryHandler(db);
        var result = await handler.Handle(new GetExamResultsReportQuery(exam.Id), CancellationToken.None);

        var row = Assert.Single(result.Value!.Rows);
        Assert.Equal(3, row.TabSwitchCount); // the later-submitted attempt won the tie
    }

    [Fact]
    public async Task Handle_ExamWithNoCompletedAttempts_ReturnsEmptySuccessReport()
    {
        var (db, exam) = SeedExam();
        // Only an in-progress attempt exists -> excluded -> zero completed candidates.
        AddAttempt(db, exam, AddCandidate(db, "InProgress", "29001010100011"), score: 70m, status: ExamAttemptStatus.InProgress);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamResultsReportQueryHandler(db);
        var result = await handler.Handle(new GetExamResultsReportQuery(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var report = result.Value!;
        Assert.Equal(0, report.Summary.TotalCandidates);
        Assert.Equal(0m, report.Summary.PassRatePercentage); // division-by-zero guard
        Assert.Empty(report.Rows);
    }

    [Fact]
    public async Task Handle_AllFailed_ReportsZeroPassRate()
    {
        var (db, exam) = SeedExam();
        AddAttempt(db, exam, AddCandidate(db, "F1", "29001010100011"), score: 10m);
        AddAttempt(db, exam, AddCandidate(db, "F2", "29001010100012"), score: 20m);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamResultsReportQueryHandler(db);
        var result = await handler.Handle(new GetExamResultsReportQuery(exam.Id), CancellationToken.None);

        var summary = result.Value!.Summary;
        Assert.Equal(2, summary.TotalCandidates);
        Assert.Equal(0, summary.PassedCount);
        Assert.Equal(2, summary.FailedCount);
        Assert.Equal(0m, summary.PassRatePercentage);
    }

    [Fact]
    public async Task Handle_PassRate_IsRoundedToTwoDecimals()
    {
        var (db, exam) = SeedExam();
        AddAttempt(db, exam, AddCandidate(db, "P1", "29001010100011"), score: 60m); // pass
        AddAttempt(db, exam, AddCandidate(db, "F1", "29001010100012"), score: 10m); // fail
        AddAttempt(db, exam, AddCandidate(db, "F2", "29001010100013"), score: 20m); // fail
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamResultsReportQueryHandler(db);
        var result = await handler.Handle(new GetExamResultsReportQuery(exam.Id), CancellationToken.None);

        var summary = result.Value!.Summary;
        Assert.Equal(1, summary.PassedCount);
        Assert.Equal(2, summary.FailedCount);
        Assert.Equal(33.33m, summary.PassRatePercentage); // 1/3 -> 33.33
    }

    [Fact]
    public async Task Handle_UnknownExam_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        var handler = new GetExamResultsReportQueryHandler(db);

        var result = await handler.Handle(new GetExamResultsReportQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
