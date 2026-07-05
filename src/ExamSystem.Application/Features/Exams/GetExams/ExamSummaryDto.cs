using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.GetExams;

public record ExamSummaryDto(
    Guid Id, string Name, DateTime StartAtUtc, DateTime EndAtUtc, int DurationMinutes,
    ExamStatus Status, int TotalQuestionCount, decimal TotalPoints);
