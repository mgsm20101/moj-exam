namespace ExamSystem.Application.Features.Exams.CreateExam;

public class CreateExamCommandValidator : AbstractValidator<CreateExamCommand>
{
    public CreateExamCommandValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Exam name is required.");
        RuleFor(x => x.EndAtUtc).GreaterThan(x => x.StartAtUtc).WithMessage("End date must be after the start date.");
        RuleFor(x => x.DurationMinutes).GreaterThan(0).WithMessage("Duration must be greater than zero.");
        RuleFor(x => x.PassMarkPercentage).InclusiveBetween(0, 100).WithMessage("Pass mark must be between 0 and 100.");
        RuleFor(x => x.MaxAttempts).GreaterThanOrEqualTo(1).WithMessage("Max attempts must be at least 1.");

        RuleFor(x => x.TopicSelections)
            .Must(selections => selections is { Count: > 0 })
            .WithMessage("At least one topic must be configured.");

        When(x => x.TopicSelections is { Count: > 0 }, () =>
        {
            RuleForEach(x => x.TopicSelections)
                .ChildRules(selection =>
                {
                    selection.RuleFor(s => s.Count).GreaterThan(0).WithMessage("Question count must be greater than zero.");
                });

            RuleFor(x => x.TopicSelections)
                .Must(selections => selections
                    .GroupBy(s => (s.TopicId, s.Difficulty, s.Type))
                    .All(g => g.Count() == 1))
                .WithMessage("Each Topic/Difficulty/Type combination can only appear once.");

            RuleForEach(x => x.TopicSelections)
                .MustAsync(async (selection, ct) => await db.Topics.AnyAsync(t => t.Id == selection.TopicId && t.IsActive, ct))
                .WithMessage("Topic not found or inactive.");
        });
    }
}
