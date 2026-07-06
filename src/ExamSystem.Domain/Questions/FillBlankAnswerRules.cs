using System.Text.RegularExpressions;

namespace ExamSystem.Domain.Questions;

/// <summary>Single source of truth for the FillBlank single-lowercase-word rule (FR-3.2.1).</summary>
public static class FillBlankAnswerRules
{
    public static readonly Regex AnswerPattern = new("^[a-z0-9]+$");

    /// <summary>Normalizes a candidate's answer before comparison (FR-2 FillBlank rules):
    /// trim, lowercase (invariant), and remove all internal whitespace.</summary>
    public static string Normalize(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return string.Empty;
        }
        return new string(answer.Trim().ToLowerInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
