namespace ExamSystem.Infrastructure.Identity;

public class AttemptTokenSettings
{
    public const string SectionName = "AttemptToken";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "ExamSystem";
    public string Audience { get; set; } = "ExamSystemCandidates";
}
