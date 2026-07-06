using ExamSystem.Domain.Candidates;
using Xunit;

namespace ExamSystem.Application.UnitTests.Domain;

public class NationalIdTests
{
    // 2 991231 01 2340 4 -> century 1900s, born 1999-12-31, Cairo(01), gender digit (index 12) = 0 -> even -> Female
    private const string Valid1999Female = "29912310123404";
    // 3 010615 21 1235 4 -> century 2000s, born 2001-06-15, Giza(21), gender digit (index 12) = 5 -> odd -> Male
    private const string Valid2001Male = "30106152112354";

    [Fact]
    public void TryParse_ValidId_DerivesBirthDateGenderAndGovernorate()
    {
        var ok = NationalId.TryParse(Valid1999Female, out var id, out var error);

        Assert.True(ok, error);
        Assert.Equal(new DateTime(1999, 12, 31, 0, 0, 0, DateTimeKind.Utc), id!.BirthDateUtc);
        Assert.Equal(Gender.Female, id.Gender);
        Assert.Equal(1, id.GovernorateCode);
        Assert.Equal(Valid1999Female, id.Value);
    }

    [Fact]
    public void TryParse_MaleOddSerial_DerivesMale()
    {
        Assert.True(NationalId.TryParse(Valid2001Male, out var id, out _));
        Assert.Equal(Gender.Male, id!.Gender);
        Assert.Equal(new DateTime(2001, 6, 15, 0, 0, 0, DateTimeKind.Utc), id.BirthDateUtc);
        Assert.Equal(21, id.GovernorateCode);
    }

    [Theory]
    [InlineData("", "National ID must be exactly 14 digits.")]
    [InlineData("123", "National ID must be exactly 14 digits.")]
    [InlineData("2991231012340X", "National ID must be exactly 14 digits.")]
    [InlineData("19912310123404", "National ID has an invalid century digit.")]      // century 1
    [InlineData("29913310123404", "National ID contains an invalid birth date.")]     // month 13
    [InlineData("29912319923404", "National ID has an invalid governorate code.")]     // gov 99
    public void TryParse_InvalidId_FailsWithMessage(string value, string expected)
    {
        var ok = NationalId.TryParse(value, out var id, out var error);

        Assert.False(ok);
        Assert.Null(id);
        Assert.Equal(expected, error);
    }
}
