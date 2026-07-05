using ExamSystem.Application.Features.Topics.UpdateTopic;

namespace ExamSystem.Application.UnitTests.Features.Topics;

public class UpdateTopicCommandValidatorTests
{
    private readonly UpdateTopicCommandValidator _validator = new();

    [Fact]
    public void Validate_EmptyName_IsRejected()
    {
        var result = _validator.Validate(new UpdateTopicCommand(Guid.NewGuid(), "", 1, true));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_NegativeDisplayOrder_IsRejected()
    {
        var result = _validator.Validate(new UpdateTopicCommand(Guid.NewGuid(), "Excel", -1, true));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ValidCommand_IsAccepted()
    {
        var result = _validator.Validate(new UpdateTopicCommand(Guid.NewGuid(), "Excel", 1, true));

        Assert.True(result.IsValid);
    }
}
