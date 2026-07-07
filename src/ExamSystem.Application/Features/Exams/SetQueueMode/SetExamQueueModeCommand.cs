using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.Exams.SetQueueMode;

public record SetExamQueueModeCommand(Guid ExamId, QueueMode Mode) : IRequest<Result<Unit>>;
