using ExamSystem.Application.Features.Questions.CreateQuestion;
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.UnitTests.Features.Questions;

public class CreateQuestionCommandValidatorTests
{
    private static CreateQuestionCommandValidator CreateValidator(ApplicationDbContextFixtureTopic topic)
    {
        var db = TestDbContextFactory.Create();
        db.Topics.Add(new ExamSystem.Domain.Topics.Topic { Id = topic.Id, Name = topic.Name, DisplayOrder = 1, IsActive = topic.IsActive });
        db.SaveChanges();
        return new CreateQuestionCommandValidator(db);
    }

    private static readonly ApplicationDbContextFixtureTopic ActiveTopic = new(Guid.NewGuid(), "Excel", true);

    [Theory]
    [InlineData("server")]
    [InlineData("counter1")]
    public async Task FillBlank_LowercaseSingleWordAnswer_IsValid(string answer)
    {
        var validator = CreateValidator(ActiveTopic);
        var command = new CreateQuestionCommand(ActiveTopic.Id, QuestionType.FillBlank, DifficultyLevel.Medium, "Fill ___", null, null, answer, null);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Data Base")]
    [InlineData("SERVER")]
    [InlineData("mail merge")]
    [InlineData("")]
    public async Task FillBlank_InvalidAnswerFormat_IsRejected(string answer)
    {
        var validator = CreateValidator(ActiveTopic);
        var command = new CreateQuestionCommand(ActiveTopic.Id, QuestionType.FillBlank, DifficultyLevel.Medium, "Fill ___", null, null, answer, null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Mcq_WithoutExactlyOneCorrectOption_IsRejected()
    {
        var validator = CreateValidator(ActiveTopic);
        var options = new List<QuestionOptionInput>
        {
            new("A", true),
            new("B", true),
            new("C", false)
        };
        var command = new CreateQuestionCommand(ActiveTopic.Id, QuestionType.Mcq, DifficultyLevel.Medium, "Pick one", null, options, null, null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Mcq_WithExactlyOneCorrectOption_IsValid()
    {
        var validator = CreateValidator(ActiveTopic);
        var options = new List<QuestionOptionInput>
        {
            new("A", false),
            new("B", true),
            new("C", false)
        };
        var command = new CreateQuestionCommand(ActiveTopic.Id, QuestionType.Mcq, DifficultyLevel.Medium, "Pick one", null, options, null, null);

        var result = await validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task InactiveTopic_IsRejected()
    {
        var inactiveTopic = new ApplicationDbContextFixtureTopic(Guid.NewGuid(), "Retired", false);
        var validator = CreateValidator(inactiveTopic);
        var command = new CreateQuestionCommand(inactiveTopic.Id, QuestionType.FillBlank, DifficultyLevel.Medium, "Fill ___", null, null, "answer", null);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }
}

public record ApplicationDbContextFixtureTopic(Guid Id, string Name, bool IsActive);
