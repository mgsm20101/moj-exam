namespace ExamSystem.Application.Features.Questions.GetQuestionBankSummary;

public record GetQuestionBankSummaryQuery : IRequest<Result<List<QuestionBankSummaryRow>>>;
