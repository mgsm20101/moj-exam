namespace ExamSystem.Application.Features.Questions.BulkImportQuestions;

public record BulkImportQuestionsCommand(Stream FileContent) : IRequest<Result<BulkImportReport>>;
