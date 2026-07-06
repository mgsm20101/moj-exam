namespace ExamSystem.Application.Features.CandidateExam.GetExamLanding;

public record GetExamLandingQuery(Guid ExamId) : IRequest<Result<ExamLandingDto>>;
