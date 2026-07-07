using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.Exams.GetExams;

public record ExamSummaryDto(
    Guid Id, string Name, DateTime StartAtUtc, DateTime EndAtUtc, int DurationMinutes,
    ExamStatus Status, QueueMode QueueMode, int TotalQuestionCount, decimal TotalPoints);
