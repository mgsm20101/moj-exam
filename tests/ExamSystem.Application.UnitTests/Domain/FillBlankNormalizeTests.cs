using ExamSystem.Domain.Questions;
using Xunit;

namespace ExamSystem.Application.UnitTests.Domain;

public class FillBlankNormalizeTests
{
    [Theory]
    [InlineData("server", "server")]
    [InlineData("  Server ", "server")]
    [InlineData("SERVER", "server")]
    [InlineData("data base", "database")]
    [InlineData("Da Ta", "data")]
    public void Normalize_TrimsLowercasesAndStripsSpaces(string input, string expected)
    {
        Assert.Equal(expected, FillBlankAnswerRules.Normalize(input));
    }

    [Fact]
    public void Normalize_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, FillBlankAnswerRules.Normalize(null));
    }
}
