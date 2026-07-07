namespace ExamSystem.Application.Features.Exams.OpenNextBatch;

public record OpenNextBatchCommand(Guid ExamId, int Count) : IRequest<Result<OpenBatchResultDto>>;
