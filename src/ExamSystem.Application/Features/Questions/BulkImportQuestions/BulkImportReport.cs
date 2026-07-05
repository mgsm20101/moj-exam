namespace ExamSystem.Application.Features.Questions.BulkImportQuestions;

public record BulkImportRowError(string Sheet, int RowNumber, string Message);

public record BulkImportReport(int TotalRows, int SuccessCount, int FailureCount, List<BulkImportRowError> Errors);
