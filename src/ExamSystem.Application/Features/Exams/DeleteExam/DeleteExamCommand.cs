namespace ExamSystem.Application.Features.Exams.DeleteExam;

public record DeleteExamCommand(Guid Id) : IRequest<Result<Unit>>;
