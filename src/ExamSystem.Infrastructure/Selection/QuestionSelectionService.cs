using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Common.Models;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Infrastructure.Selection;

public class QuestionSelectionService : IQuestionSelectionService
{
    private static readonly DifficultyLevel[] DifficultyOrder =
        { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard };

    private readonly IApplicationDbContext _db;

    public QuestionSelectionService(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<AttemptQuestion>>> BuildSnapshotAsync(
        Exam exam, int seed, CancellationToken cancellationToken)
    {
        var random = new Random(seed);
        var snapshot = new List<AttemptQuestion>();
        var order = 1;

        // Pull every candidate question for the exam's topics in a single no-tracking round-trip,
        // then bucket by (topic, type, difficulty) in memory. This replaces one tracked query per
        // topic-selection (N round-trips + heavy change-tracking) — the dominant cost of "start exam".
        var topicIds = exam.TopicSelections.Select(s => s.TopicId).Distinct().ToList();
        var pool = await _db.Questions
            .AsNoTracking()
            .Include(q => q.Options)
            .Where(q => topicIds.Contains(q.TopicId) && q.IsActive)
            .ToListAsync(cancellationToken);

        // Order each bucket by Id so the seeded shuffle below is reproducible (matches the old
        // per-selection query's "ORDER BY Id"), keeping question selection deterministic per seed.
        var byKey = pool
            .GroupBy(q => (q.TopicId, q.Type, q.Difficulty))
            .ToDictionary(g => g.Key, g => g.OrderBy(q => q.Id).ToList());

        foreach (var selection in exam.TopicSelections
                     .OrderBy(s => s.DisplayOrder)
                     .ThenBy(s => Array.IndexOf(DifficultyOrder, s.Difficulty)))
        {
            var available = byKey.TryGetValue((selection.TopicId, selection.Type, selection.Difficulty), out var group)
                ? group
                : new List<Question>();

            if (available.Count < selection.Count)
            {
                return Result<List<AttemptQuestion>>.Failure(
                    $"Insufficient questions for topic {selection.TopicId} ({selection.Difficulty}/{selection.Type}): " +
                    $"need {selection.Count}, have {available.Count}.");
            }

            foreach (var question in available.OrderBy(_ => random.Next()).Take(selection.Count))
            {
                snapshot.Add(new AttemptQuestion
                {
                    SourceQuestionId = question.Id,
                    TopicId = question.TopicId,
                    DisplayOrder = order++,
                    Type = question.Type,
                    Difficulty = question.Difficulty,
                    TextSnapshot = question.Text,
                    ImageUrlSnapshot = question.ImageUrl,
                    CorrectAnswerTextSnapshot = question.CorrectAnswerText,
                    Options = question.Options
                        .OrderBy(o => o.DisplayOrder)
                        .Select(o => new AttemptQuestionOption
                        {
                            TextSnapshot = o.Text,
                            IsCorrect = o.IsCorrect,
                            DisplayOrder = o.DisplayOrder
                        })
                        .ToList()
                });
            }
        }

        return Result<List<AttemptQuestion>>.Success(snapshot);
    }
}
