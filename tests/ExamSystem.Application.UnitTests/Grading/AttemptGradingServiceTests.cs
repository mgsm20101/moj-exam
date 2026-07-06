using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Infrastructure.Grading;
using Xunit;

namespace ExamSystem.Application.UnitTests.Grading;

public class AttemptGradingServiceTests
{
    private static Exam Exam() => new()
    {
        McqPoints = 2m, TrueFalsePoints = 1m, FillBlankPoints = 5m, PassMarkPercentage = 60m
    };

    private static (AttemptQuestion q, Guid correctOptionId) McqQuestion(int order)
    {
        var correct = new AttemptQuestionOption { TextSnapshot = "right", IsCorrect = true, DisplayOrder = 1 };
        var wrong = new AttemptQuestionOption { TextSnapshot = "wrong", IsCorrect = false, DisplayOrder = 2 };
        var q = new AttemptQuestion
        {
            DisplayOrder = order, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium,
            TextSnapshot = "q", Options = new List<AttemptQuestionOption> { correct, wrong }
        };
        correct.AttemptQuestionId = q.Id; wrong.AttemptQuestionId = q.Id;
        return (q, correct.Id);
    }

    private static AttemptQuestion FillBlankQuestion(int order, string answer)
    {
        return new AttemptQuestion
        {
            DisplayOrder = order, Type = QuestionType.FillBlank, Difficulty = DifficultyLevel.Hard,
            TextSnapshot = "fb", CorrectAnswerTextSnapshot = answer
        };
    }

    [Fact]
    public void Grade_AllCorrect_ScoresFullAndPasses()
    {
        var (mcq, correctId) = McqQuestion(1);
        var fb = FillBlankQuestion(2, "server");
        var attempt = new ExamAttempt { Questions = { mcq, fb } };
        attempt.Answers.Add(new AttemptAnswer { AttemptQuestionId = mcq.Id, SelectedOptionId = correctId });
        attempt.Answers.Add(new AttemptAnswer { AttemptQuestionId = fb.Id, AnswerText = " SERVER " });

        var result = new AttemptGradingService().Grade(attempt, Exam());

        Assert.Equal(7m, result.Score);        // 2 + 5
        Assert.Equal(7m, result.TotalPoints);
        Assert.True(result.Passed);
        Assert.All(attempt.Answers, a => Assert.True(a.IsCorrect));
    }

    [Fact]
    public void Grade_MixedAndUnanswered_ScoresPartialAndFails()
    {
        var (mcq, correctId) = McqQuestion(1);
        var fb = FillBlankQuestion(2, "server");
        var attempt = new ExamAttempt { Questions = { mcq, fb } };
        // MCQ answered wrong; FillBlank unanswered
        var wrongOptionId = mcq.Options.First(o => !o.IsCorrect).Id;
        attempt.Answers.Add(new AttemptAnswer { AttemptQuestionId = mcq.Id, SelectedOptionId = wrongOptionId });

        var result = new AttemptGradingService().Grade(attempt, Exam());

        Assert.Equal(0m, result.Score);
        Assert.Equal(7m, result.TotalPoints);
        Assert.False(result.Passed);
    }

    [Fact]
    public void Grade_FillBlankCaseAndSpaceInsensitive()
    {
        var fb = FillBlankQuestion(1, "database");
        var attempt = new ExamAttempt { Questions = { fb } };
        attempt.Answers.Add(new AttemptAnswer { AttemptQuestionId = fb.Id, AnswerText = "Data Base" });

        var result = new AttemptGradingService().Grade(attempt, Exam());

        Assert.Equal(5m, result.Score);
        Assert.True(attempt.Answers.Single().IsCorrect);
    }
}
