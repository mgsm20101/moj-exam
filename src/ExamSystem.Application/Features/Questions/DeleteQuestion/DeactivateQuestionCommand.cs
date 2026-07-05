namespace ExamSystem.Application.Features.Questions.DeleteQuestion;

public record DeactivateQuestionCommand(Guid Id) : IRequest<Result<Unit>>;
