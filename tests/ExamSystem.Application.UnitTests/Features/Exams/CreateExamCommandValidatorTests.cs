using ExamSystem.Application.Features.Exams;
using ExamSystem.Application.Features.Exams.CreateExam;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class CreateExamCommandValidatorTests
{
    private static (CreateExamCommandValidator Validator, Guid TopicId) CreateValidatorWithActiveTopic()
    {
        var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1, IsActive = true };
        db.Topics.Add(topic);
        db.SaveChanges();
        return (new CreateExamCommandValidator(db), topic.Id);
    }

    private static CreateExamCommand ValidCommand(Guid topicId, List<ExamTopicSelectionInput>? selections = null) =>
        new(
            "Excel Basics", null, DateTime.UtcNow, DateTime.UtcNow.AddDays(7), 60,
            2m, 1m, 5m, 60m, 1, true, true, true,
            selections ?? new List<ExamTopicSelectionInput> { new(topicId, 1, DifficultyLevel.Medium, QuestionType.Mcq, 25) });

    [Fact]
    public async Task ValidCommand_IsValid()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();

        var result = await validator.ValidateAsync(ValidCommand(topicId));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task EndBeforeStart_IsRejected()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(topicId) with { StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(-1) };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task NoTopicSelections_IsRejected()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(topicId, new List<ExamTopicSelectionInput>());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ZeroCountSelection_IsRejected()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(topicId, new List<ExamTopicSelectionInput> { new(topicId, 1, DifficultyLevel.Medium, QuestionType.Mcq, 0) });

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task DuplicateTopicDifficultyType_IsRejected()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(topicId, new List<ExamTopicSelectionInput>
        {
            new(topicId, 1, DifficultyLevel.Medium, QuestionType.Mcq, 10),
            new(topicId, 1, DifficultyLevel.Medium, QuestionType.Mcq, 5)
        });

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task UnknownTopic_IsRejected()
    {
        var (validator, _) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task PassMarkOutOfRange_IsRejected()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(topicId) with { PassMarkPercentage = 150m };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }
}
