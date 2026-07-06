namespace ExamSystem.Application.Features.Exams.GetExamLiveCounts;

public record GetExamLiveCountsQuery : IRequest<Result<List<ExamLiveCountsDto>>>;
