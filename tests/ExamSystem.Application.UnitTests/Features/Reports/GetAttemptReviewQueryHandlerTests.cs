using ExamSystem.Application.Features.Reports.GetAttemptReview;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.Reports;

public class GetAttemptReviewQueryHandlerTests
{
    // Seeds one attempt with three MCQ questions: Q1 answered correctly, Q2 answered wrong, Q3 unanswered.
    private static async Task<(Infrastructure.Persistence.ApplicationDbContext db, ExamAttempt attempt)>
        SeedAsync(bool showResult)
    {
        var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "E", DurationMinutes = 60, McqPoints = 2m, PassMarkPercentage = 60m, ShowResultImmediately = showResult };
        db.Exams.Add(exam);

        var candidate = new Candidate
        {
            FullName = "علي محمد",
            NationalId = "12345678901234",
            MobileNumber = "01000000000",
            BirthDateUtc = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            GovernorateCode = 1
        };
        db.Candidates.Add(candidate);

        var attempt = new ExamAttempt
        {
            ExamId = exam.Id,
            CandidateId = candidate.Id,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
            Status = ExamAttemptStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            Score = 2m
        };

        var q1Correct = new AttemptQuestionOption { TextSnapshot = "Q1-right", IsCorrect = true, DisplayOrder = 1 };
        var q1Wrong = new AttemptQuestionOption { TextSnapshot = "Q1-wrong", IsCorrect = false, DisplayOrder = 2 };
        var q1 = new AttemptQuestion { DisplayOrder = 1, Type = QuestionType.Mcq, TextSnapshot = "Q1", Options = { q1Correct, q1Wrong } };

        var q2Correct = new AttemptQuestionOption { TextSnapshot = "Q2-right", IsCorrect = true, DisplayOrder = 1 };
        var q2Wrong = new AttemptQuestionOption { TextSnapshot = "Q2-wrong", IsCorrect = false, DisplayOrder = 2 };
        var q2 = new AttemptQuestion { DisplayOrder = 2, Type = QuestionType.Mcq, TextSnapshot = "Q2", Options = { q2Correct, q2Wrong } };

        var q3 = new AttemptQuestion { DisplayOrder = 3, Type = QuestionType.Mcq, TextSnapshot = "Q3", Options =
            { new AttemptQuestionOption { TextSnapshot = "Q3-right", IsCorrect = true, DisplayOrder = 1 } } };

        attempt.Questions.Add(q1);
        attempt.Questions.Add(q2);
        attempt.Questions.Add(q3);
        attempt.Answers.Add(new AttemptAnswer { AttemptId = attempt.Id, AttemptQuestionId = q1.Id, SelectedOptionId = q1Correct.Id, IsCorrect = true });
        attempt.Answers.Add(new AttemptAnswer { AttemptId = attempt.Id, AttemptQuestionId = q2.Id, SelectedOptionId = q2Wrong.Id, IsCorrect = false });
        // Q3 intentionally has no AttemptAnswer.
        db.ExamAttempts.Add(attempt);

        await db.SaveChangesAsync(CancellationToken.None);
        return (db, attempt);
    }

    [Fact]
    public async Task Handle_ReturnsPerQuestionCorrectness_IncludingUnanswered()
    {
        var (db, attempt) = await SeedAsync(showResult: true);
        var handler = new GetAttemptReviewQueryHandler(db);

        var result = await handler.Handle(
            new GetAttemptReviewQuery(attempt.Id, EnforceShowResultGate: false, RevealCorrectAnswers: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.True(dto.Shown);
        Assert.Equal("علي محمد", dto.CandidateName);
        Assert.Equal(3, dto.Questions.Count);

        var q1 = dto.Questions.Single(q => q.Text == "Q1");
        Assert.True(q1.IsCorrect);
        Assert.True(q1.WasAnswered);
        Assert.Contains(q1.Options, o => o.IsCorrect && o.WasSelected);

        var q2 = dto.Questions.Single(q => q.Text == "Q2");
        Assert.False(q2.IsCorrect);
        Assert.True(q2.WasAnswered);
        Assert.Contains(q2.Options, o => o.WasSelected && !o.IsCorrect);

        var q3 = dto.Questions.Single(q => q.Text == "Q3");
        Assert.False(q3.IsCorrect);
        Assert.False(q3.WasAnswered);
    }

    [Fact]
    public async Task Handle_WithdrawsQuestions_ForCandidateWhenShowResultImmediatelyFalse()
    {
        var (db, attempt) = await SeedAsync(showResult: false);
        var handler = new GetAttemptReviewQueryHandler(db);

        var result = await handler.Handle(
            new GetAttemptReviewQuery(attempt.Id, EnforceShowResultGate: true, RevealCorrectAnswers: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Shown);
        Assert.Empty(result.Value.Questions);
    }

    [Fact]
    public async Task Handle_ForCandidate_HidesCorrectAnswersButKeepsVerdict()
    {
        var (db, attempt) = await SeedAsync(showResult: true);
        var handler = new GetAttemptReviewQueryHandler(db);

        var result = await handler.Handle(
            new GetAttemptReviewQuery(attempt.Id, EnforceShowResultGate: true, RevealCorrectAnswers: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.True(dto.Shown);
        // The candidate never learns which option is correct...
        Assert.All(dto.Questions, q => Assert.DoesNotContain(q.Options, o => o.IsCorrect));
        Assert.All(dto.Questions, q => Assert.Null(q.CorrectAnswerText));
        // ...but still sees the verdict on their own answer and which option they chose.
        var q1 = dto.Questions.Single(q => q.Text == "Q1");
        Assert.True(q1.IsCorrect);
        Assert.Contains(q1.Options, o => o.WasSelected);
        var q2 = dto.Questions.Single(q => q.Text == "Q2");
        Assert.False(q2.IsCorrect);
        Assert.Contains(q2.Options, o => o.WasSelected);
    }

    [Fact]
    public async Task Handle_AdminAlwaysSeesQuestions_EvenWhenShowResultImmediatelyFalse()
    {
        var (db, attempt) = await SeedAsync(showResult: false);
        var handler = new GetAttemptReviewQueryHandler(db);

        var result = await handler.Handle(
            new GetAttemptReviewQuery(attempt.Id, EnforceShowResultGate: false, RevealCorrectAnswers: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Shown);
        Assert.Equal(3, result.Value.Questions.Count);
    }
}
