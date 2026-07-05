namespace ExamSystem.Application.Features.Exams.GetExams;

public record GetExamsQuery : IRequest<Result<List<ExamSummaryDto>>>;
