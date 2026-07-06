using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.CandidateExam.GetExamLanding;

public class GetExamLandingQueryHandler : IRequestHandler<GetExamLandingQuery, Result<ExamLandingDto>>
{
    private readonly IApplicationDbContext _db;

    public GetExamLandingQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ExamLandingDto>> Handle(GetExamLandingQuery request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .Include(e => e.TopicSelections)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);

        if (exam is null)
        {
            return Result<ExamLandingDto>.Failure("Exam not found.");
        }

        var now = DateTime.UtcNow;
        var isOpen = exam.Status == ExamStatus.Published && now >= exam.StartAtUtc && now <= exam.EndAtUtc;
        var totalQuestions = exam.TopicSelections.Sum(s => s.Count);

        return Result<ExamLandingDto>.Success(new ExamLandingDto(
            exam.Id, exam.Name, exam.Description, isOpen, exam.DurationMinutes, totalQuestions));
    }
}
