using ExamSystem.Application;
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Questions.BulkImportQuestions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ExamSystem.Application.UnitTests.Features.Questions;

public class BulkImportQuestionsCommandHandlerTests
{
    private static ISender BuildRealSender(ExamSystem.Infrastructure.Persistence.ApplicationDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDbContext>(db);
        services.AddApplication();
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Handle_MixOfValidAndInvalidRows_ImportsValidOnesAndReportsFailures()
    {
        using var db = TestDbContextFactory.Create();
        var sender = BuildRealSender(db);

        var workbook = new ParsedQuestionWorkbook(
            McqRows: new List<ParsedMcqRow>
            {
                new(2, "Excel", "Medium", "What does CPU stand for?", "Central Processing Unit", "Cool Processor Utility", "Compact Power Unit", "Core Processing Unit", "A")
            },
            FillBlankRows: new List<ParsedFillBlankRow>
            {
                new(2, "Excel", "Medium", "Fill ___ (valid)", "server"),
                new(3, "Excel", "Medium", "Fill ___ (invalid, has a space)", "data base")
            });

        var parser = new Mock<IExcelQuestionParser>();
        parser.Setup(p => p.Parse(It.IsAny<Stream>())).Returns(workbook);

        var handler = new BulkImportQuestionsCommandHandler(db, sender, parser.Object);
        var result = await handler.Handle(new BulkImportQuestionsCommand(Stream.Null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.TotalRows);
        Assert.Equal(2, result.Value.SuccessCount);
        Assert.Equal(1, result.Value.FailureCount);
        Assert.Single(result.Value.Errors, e => e.Sheet == "FillBlank" && e.RowNumber == 3);
        Assert.Equal(2, db.Questions.Count());
    }

    [Fact]
    public async Task Handle_UnknownTopic_CreatesItAutomatically()
    {
        using var db = TestDbContextFactory.Create();
        var sender = BuildRealSender(db);

        var workbook = new ParsedQuestionWorkbook(
            McqRows: new List<ParsedMcqRow>(),
            FillBlankRows: new List<ParsedFillBlankRow> { new(2, "Brand New Topic", "Hard", "Fill ___", "answer") });

        var parser = new Mock<IExcelQuestionParser>();
        parser.Setup(p => p.Parse(It.IsAny<Stream>())).Returns(workbook);

        var handler = new BulkImportQuestionsCommandHandler(db, sender, parser.Object);
        var result = await handler.Handle(new BulkImportQuestionsCommand(Stream.Null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.SuccessCount);
        Assert.Contains(db.Topics, t => t.Name == "Brand New Topic");
    }
}
