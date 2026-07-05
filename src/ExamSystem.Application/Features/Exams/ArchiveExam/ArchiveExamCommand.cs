namespace ExamSystem.Application.Features.Exams.ArchiveExam;

public record ArchiveExamCommand(Guid Id) : IRequest<Result<Unit>>;
