using ExamSystem.Application.Common.Behaviors;
using ExamSystem.Application.Common.Models;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using Xunit;

namespace ExamSystem.Application.UnitTests.Behaviors;

public record SampleRequest(string Name) : IRequest<Result<string>>;

public class SampleRequestValidator : AbstractValidator<SampleRequest>
{
    public SampleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
    }
}

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<SampleRequest, Result<string>>(Enumerable.Empty<IValidator<SampleRequest>>());
        var nextCalled = false;

        var response = await behavior.Handle(new SampleRequest("x"), () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<string>.Success("ok"));
        }, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(response.IsSuccess);
    }

    [Fact]
    public async Task Handle_ValidationFails_ReturnsFailureResultWithoutCallingNext()
    {
        var behavior = new ValidationBehavior<SampleRequest, Result<string>>(new[] { new SampleRequestValidator() });
        var nextCalled = false;

        var response = await behavior.Handle(new SampleRequest(""), () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<string>.Success("should not happen"));
        }, CancellationToken.None);

        Assert.False(nextCalled);
        Assert.False(response.IsSuccess);
        Assert.Contains("Name is required.", response.Errors);
    }

    [Fact]
    public async Task Handle_ValidationPasses_CallsNext()
    {
        var behavior = new ValidationBehavior<SampleRequest, Result<string>>(new[] { new SampleRequestValidator() });

        var response = await behavior.Handle(new SampleRequest("Ali"), () => Task.FromResult(Result<string>.Success("ok")), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal("ok", response.Value);
    }
}
