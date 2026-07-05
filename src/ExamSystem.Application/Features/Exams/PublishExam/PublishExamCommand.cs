namespace ExamSystem.Application.Features.Exams.PublishExam;

public record PublishExamCommand(Guid Id) : IRequest<Result<Unit>>;
