namespace ExamSystem.Application.Features.Exams.GetExamById;

public record GetExamByIdQuery(Guid Id) : IRequest<Result<ExamDetailDto>>;
