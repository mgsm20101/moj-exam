namespace ExamSystem.Application.Common.Models;

public record GradeResult(decimal Score, decimal TotalPoints, decimal PassMarkPercentage, bool Passed);
