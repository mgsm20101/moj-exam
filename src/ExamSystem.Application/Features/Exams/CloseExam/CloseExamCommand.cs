namespace ExamSystem.Application.Features.Exams.CloseExam;

public record CloseExamCommand(Guid Id) : IRequest<Result<Unit>>;
