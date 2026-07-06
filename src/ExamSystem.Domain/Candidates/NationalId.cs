using System.Globalization;

namespace ExamSystem.Domain.Candidates;

/// <summary>
/// Egyptian National ID value object. Structure: C YYMMDD GG NNNN S
/// (century, birth date, governorate, serial, check digit). We validate structure only
/// (FR-1.2): 14 digits, century 2 or 3, a real birth date, and a known governorate code.
/// The check digit is not algorithmically verified in v1.
/// </summary>
public sealed class NationalId
{
    public string Value { get; }
    public DateTime BirthDateUtc { get; }
    public Gender Gender { get; }
    public int GovernorateCode { get; }

    private NationalId(string value, DateTime birthDateUtc, Gender gender, int governorateCode)
    {
        Value = value;
        BirthDateUtc = birthDateUtc;
        Gender = gender;
        GovernorateCode = governorateCode;
    }

    public static bool TryParse(string? input, out NationalId? id, out string? error)
    {
        id = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input) || input.Length != 14 || !input.All(char.IsDigit))
        {
            error = "National ID must be exactly 14 digits.";
            return false;
        }

        var century = input[0] switch { '2' => 1900, '3' => 2000, _ => -1 };
        if (century == -1)
        {
            error = "National ID has an invalid century digit.";
            return false;
        }

        var year = century + int.Parse(input.Substring(1, 2), CultureInfo.InvariantCulture);
        var month = int.Parse(input.Substring(3, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(input.Substring(5, 2), CultureInfo.InvariantCulture);
        if (!TryBuildDate(year, month, day, out var birthDate))
        {
            error = "National ID contains an invalid birth date.";
            return false;
        }

        var governorate = int.Parse(input.Substring(7, 2), CultureInfo.InvariantCulture);
        if (!((governorate >= 1 && governorate <= 35) || governorate == 88))
        {
            error = "National ID has an invalid governorate code.";
            return false;
        }

        var genderDigit = input[12] - '0';
        var gender = genderDigit % 2 == 1 ? Gender.Male : Gender.Female;

        id = new NationalId(input, birthDate, gender, governorate);
        return true;
    }

    private static bool TryBuildDate(int year, int month, int day, out DateTime date)
    {
        date = default;
        if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }
        date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        return true;
    }
}
