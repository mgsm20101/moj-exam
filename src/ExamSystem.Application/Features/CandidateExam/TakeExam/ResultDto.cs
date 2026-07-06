namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

/// <summary>When Shown is false the score fields are withheld (Exam.ShowResultImmediately == false).</summary>
public record ResultDto(bool Shown, decimal Score, decimal TotalPoints, decimal PassMarkPercentage, bool Passed);
