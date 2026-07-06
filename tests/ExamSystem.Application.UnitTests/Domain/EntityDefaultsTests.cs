using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using Xunit;

namespace ExamSystem.Application.UnitTests.Domain;

public class EntityDefaultsTests
{
    [Fact]
    public void ExamAttempt_Defaults_AreInProgressWithEmptyCollections()
    {
        var attempt = new ExamAttempt();

        Assert.Equal(ExamAttemptStatus.InProgress, attempt.Status);
        Assert.NotEqual(System.Guid.Empty, attempt.Id);
        Assert.Empty(attempt.Questions);
    }

    [Fact]
    public void Candidate_Defaults_HaveGeneratedIdAndEmptyGrants()
    {
        var candidate = new Candidate();

        Assert.NotEqual(System.Guid.Empty, candidate.Id);
        Assert.Empty(candidate.Grants);
    }
}
