using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.UpdateQuestion;

public class UpdateQuestionCommandValidator : AbstractValidator<UpdateQuestionCommand>
{
    public UpdateQuestionCommandValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Text).NotEmpty().WithMessage("Question text is required.");

        RuleFor(x => x.TopicId)
            .MustAsync(async (topicId, ct) => await db.Topics.AnyAsync(t => t.Id == topicId && t.IsActive, ct))
            .WithMessage("Topic not found or inactive.");

        When(x => x.Type == QuestionType.FillBlank, () =>
        {
            RuleFor(x => x.CorrectAnswerText)
                .NotEmpty()
                .Must(answer => FillBlankAnswerRules.AnswerPattern.IsMatch(answer ?? string.Empty))
                .WithMessage("FillBlank answer must be a single lowercase word (letters/digits only, no spaces).");

            RuleFor(x => x.Options).Empty().WithMessage("FillBlank questions must not have options.");
        });

        When(x => x.Type != QuestionType.FillBlank, () =>
        {
            RuleFor(x => x.CorrectAnswerText).Empty().WithMessage("CorrectAnswerText only applies to FillBlank questions.");
            RuleFor(x => x.Options).Must(options => options is { Count: >= 2 }).WithMessage("At least 2 options are required.");
            RuleFor(x => x.Options)
                .Must(options => options!.Count(o => o.IsCorrect) == 1)
                .When(x => x.Options is { Count: >= 2 })
                .WithMessage("Exactly one option must be marked correct.");
        });
    }
}
