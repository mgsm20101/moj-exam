using System.Text.RegularExpressions;
using ExamSystem.Domain.Candidates;

namespace ExamSystem.Application.Features.CandidateExam.RegisterCandidate;

public class RegisterCandidateCommandValidator : AbstractValidator<RegisterCandidateCommand>
{
    public RegisterCandidateCommandValidator()
    {
        RuleFor(x => x.FullName)
            .Must(name => !string.IsNullOrWhiteSpace(name)
                          && name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 4)
            .WithMessage("Full name must contain at least four words.");

        RuleFor(x => x.NationalId)
            .Must(value => NationalId.TryParse(value, out _, out _))
            .WithMessage("National ID is invalid.");

        RuleFor(x => x.MobileNumber)
            .Matches(new Regex(@"^01[0125]\d{8}$"))
            .WithMessage("Mobile number must be 11 digits starting with 010, 011, 012, or 015.");
    }
}
