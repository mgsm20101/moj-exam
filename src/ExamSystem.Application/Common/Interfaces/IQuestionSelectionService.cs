using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Common.Interfaces;

/// <summary>
/// Builds the ordered, immutable question snapshot for one attempt (FR-2.2/2.3/2.5):
/// per topic (by DisplayOrder), per difficulty (Easy→Medium→Hard), a seeded random sample.
/// Returns a failure if any pool has fewer active questions than required.
/// </summary>
public interface IQuestionSelectionService
{
    Task<Result<List<AttemptQuestion>>> BuildSnapshotAsync(Exam exam, int seed, CancellationToken cancellationToken);
}
