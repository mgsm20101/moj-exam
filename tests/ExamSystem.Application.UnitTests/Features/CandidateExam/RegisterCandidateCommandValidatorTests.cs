using ExamSystem.Application.Features.CandidateExam.RegisterCandidate;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class RegisterCandidateCommandValidatorTests
{
    private readonly RegisterCandidateCommandValidator _validator = new();

    private static RegisterCandidateCommand Cmd(string name, string nid, string mobile) =>
        new(System.Guid.NewGuid(), name, nid, mobile);

    [Fact]
    public void Valid_Passes()
    {
        var result = _validator.Validate(Cmd("احمد محمد علي حسن", "29912310123404", "01012345678"));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("احمد محمد", "29912310123404", "01012345678")]   // 2 words
    [InlineData("احمد محمد علي حسن", "123", "01012345678")]        // bad NID
    [InlineData("احمد محمد علي حسن", "29912310123404", "01312345678")] // bad mobile prefix
    public void Invalid_Fails(string name, string nid, string mobile)
    {
        var result = _validator.Validate(Cmd(name, nid, mobile));
        Assert.False(result.IsValid);
    }
}
