namespace ExamSystem.Application.Features.Exams.OpenNextBatch;

public class OpenNextBatchCommandValidator : AbstractValidator<OpenNextBatchCommand>
{
    public OpenNextBatchCommandValidator()
    {
        RuleFor(x => x.Count).GreaterThanOrEqualTo(1).WithMessage("Batch count must be at least 1.");
    }
}
