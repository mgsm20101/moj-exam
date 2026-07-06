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

        foreach (var selection in exam.TopicSelections
                     .OrderBy(s => s.DisplayOrder)
                     .ThenBy(s => Array.IndexOf(DifficultyOrder, s.Difficulty)))
        {
            var pool = await _db.Questions
                .Include(q => q.Options)
                .Where(q => q.TopicId == selection.TopicId
                            && q.Type == selection.Type
                            && q.Difficulty == selection.Difficulty
                            && q.IsActive)
                .ToListAsync(cancellationToken);

            if (pool.Count < selection.Count)
            {
                return Result<List<AttemptQuestion>>.Failure(
                    $"Insufficient questions for topic {selection.TopicId} ({selection.Difficulty}/{selection.Type}): " +
                    $"need {selection.Count}, have {pool.Count}.");
            }

            foreach (var question in pool.OrderBy(_ => random.Next()).Take(selection.Count))
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
