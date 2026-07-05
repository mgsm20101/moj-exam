using System.Text.RegularExpressions;

namespace ExamSystem.Domain.Questions;

/// <summary>Single source of truth for the FillBlank single-lowercase-word rule (FR-3.2.1).</summary>
public static class FillBlankAnswerRules
{
    public static readonly Regex AnswerPattern = new("^[a-z0-9]+$");
}
