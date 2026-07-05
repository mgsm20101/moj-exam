namespace ExamSystem.Application.Features.Questions.DeleteQuestion;

public class DeactivateQuestionCommandHandler : IRequestHandler<DeactivateQuestionCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public DeactivateQuestionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(DeactivateQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);
        if (question is null)
        {
            return Result<Unit>.Failure("Question not found.");
        }

        question.IsActive = false;
        question.ModifiedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
