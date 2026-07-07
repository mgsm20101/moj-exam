using ExamSystem.Domain.Candidates;

namespace ExamSystem.Application.Features.Candidates.GrantRetake;

public class GrantCandidateRetakeCommandValidator : AbstractValidator<GrantCandidateRetakeCommand>
{
    public GrantCandidateRetakeCommandValidator()
    {
        RuleFor(x => x.ExamId).NotEmpty();

        RuleFor(x => x.NationalId)
            .Must(value => NationalId.TryParse(value, out _, out _))
            .WithMessage("National ID is invalid.");
    }
}
