using ExamSystem.Application.Features.Questions.CreateQuestion;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.Features.Questions.BulkImportQuestions;

public class BulkImportQuestionsCommandHandler : IRequestHandler<BulkImportQuestionsCommand, Result<BulkImportReport>>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;
    private readonly IExcelQuestionParser _parser;

    public BulkImportQuestionsCommandHandler(IApplicationDbContext db, ISender sender, IExcelQuestionParser parser)
    {
        _db = db;
        _sender = sender;
        _parser = parser;
    }

    public async Task<Result<BulkImportReport>> Handle(BulkImportQuestionsCommand request, CancellationToken cancellationToken)
    {
        var workbook = _parser.Parse(request.FileContent);
        var topicCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<BulkImportRowError>();
        var successCount = 0;

        foreach (var row in workbook.McqRows)
        {
            var outcome = await ImportMcqRowAsync(row, topicCache, cancellationToken);
            if (outcome is null) successCount++; else errors.Add(outcome);
        }

        foreach (var row in workbook.FillBlankRows)
        {
            var outcome = await ImportFillBlankRowAsync(row, topicCache, cancellationToken);
            if (outcome is null) successCount++; else errors.Add(outcome);
        }

        var total = workbook.McqRows.Count + workbook.FillBlankRows.Count;
        return Result<BulkImportReport>.Success(new BulkImportReport(total, successCount, total - successCount, errors));
    }

    private async Task<BulkImportRowError?> ImportMcqRowAsync(ParsedMcqRow row, Dictionary<string, Guid> topicCache, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DifficultyLevel>(row.Difficulty, ignoreCase: true, out var difficulty))
        {
            return new BulkImportRowError("MCQ", row.RowNumber, $"Unknown difficulty: {row.Difficulty}");
        }

        var correctIndex = row.CorrectOption.Trim().ToUpperInvariant() switch
        {
            "A" => 0, "B" => 1, "C" => 2, "D" => 3,
            _ => -1
        };
        if (correctIndex < 0)
        {
            return new BulkImportRowError("MCQ", row.RowNumber, $"Unknown correct option: {row.CorrectOption}");
        }

        var topicId = await ResolveOrCreateTopicAsync(row.Topic, topicCache, cancellationToken);
        var optionTexts = new[] { row.OptionA, row.OptionB, row.OptionC, row.OptionD };
        var options = optionTexts
            .Select((text, index) => new QuestionOptionInput(text, index == correctIndex))
            .ToList();

        var command = new CreateQuestionCommand(topicId, QuestionType.Mcq, difficulty, row.QuestionText, null, options, null, null);
        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess ? null : new BulkImportRowError("MCQ", row.RowNumber, string.Join("; ", result.Errors));
    }

    private async Task<BulkImportRowError?> ImportFillBlankRowAsync(ParsedFillBlankRow row, Dictionary<string, Guid> topicCache, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DifficultyLevel>(row.Difficulty, ignoreCase: true, out var difficulty))
        {
            return new BulkImportRowError("FillBlank", row.RowNumber, $"Unknown difficulty: {row.Difficulty}");
        }

        var topicId = await ResolveOrCreateTopicAsync(row.Topic, topicCache, cancellationToken);
        var command = new CreateQuestionCommand(topicId, QuestionType.FillBlank, difficulty, row.QuestionText, null, null, row.CorrectAnswer, null);
        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess ? null : new BulkImportRowError("FillBlank", row.RowNumber, string.Join("; ", result.Errors));
    }

    private async Task<Guid> ResolveOrCreateTopicAsync(string name, Dictionary<string, Guid> topicCache, CancellationToken cancellationToken)
    {
        if (topicCache.TryGetValue(name, out var cachedId))
        {
            return cachedId;
        }

        var existing = await _db.Topics.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
        if (existing is not null)
        {
            topicCache[name] = existing.Id;
            return existing.Id;
        }

        var maxOrder = await _db.Topics.Select(t => (int?)t.DisplayOrder).MaxAsync(cancellationToken) ?? 0;
        var topic = new Topic { Name = name, DisplayOrder = maxOrder + 1, IsActive = true };
        _db.Topics.Add(topic);
        await _db.SaveChangesAsync(cancellationToken);

        topicCache[name] = topic.Id;
        return topic.Id;
    }
}
