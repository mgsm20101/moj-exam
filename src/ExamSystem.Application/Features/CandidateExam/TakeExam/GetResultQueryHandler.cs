namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public class GetResultQueryHandler : IRequestHandler<GetResultQuery, Result<ResultDto>>
{
    private readonly IApplicationDbContext _db;

    public GetResultQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ResultDto>> Handle(GetResultQuery request, CancellationToken cancellationToken)
    {
        var attempt = await _db.ExamAttempts
            .Include(a => a.Questions)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null)
        {
            return Result<ResultDto>.Failure("Attempt not found.");
        }

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<ResultDto>.Failure("Exam not found.");
        }

        return Result<ResultDto>.Success(SubmitAttemptCommandHandler.BuildResult(attempt, exam));
    }
}
