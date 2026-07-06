using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Common.Interfaces;

/// <summary>
/// Grades an attempt's answers against its immutable snapshot (FR-2.13). Mutates each
/// AttemptAnswer.IsCorrect and returns the totals. Points come from the exam's per-type values.
/// The attempt must be loaded with Questions (+Options) and Answers.
/// </summary>
public interface IAttemptGradingService
{
    GradeResult Grade(ExamAttempt attempt, Exam exam);
}
