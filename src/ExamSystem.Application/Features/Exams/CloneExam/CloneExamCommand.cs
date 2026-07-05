namespace ExamSystem.Application.Features.Exams.CloneExam;

public record CloneExamCommand(Guid Id) : IRequest<Result<Guid>>;
