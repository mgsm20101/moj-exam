namespace ExamSystem.Application.Features.Exams.ReopenExam;

/// <summary>Admin "start" action (client note 6): re-activates a Closed exam back to Published.</summary>
public record ReopenExamCommand(Guid Id) : IRequest<Result<Unit>>;
