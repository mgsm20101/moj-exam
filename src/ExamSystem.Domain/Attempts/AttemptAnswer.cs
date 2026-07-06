namespace ExamSystem.Domain.Attempts;

/// <summary>A candidate's answer to one snapshot question. IsCorrect is set at grading time.</summary>
public class AttemptAnswer : BaseEntity
{
    public Guid AttemptId { get; set; }
    public Guid AttemptQuestionId { get; set; }

    /// <summary>MCQ / TrueFalse: the chosen AttemptQuestionOption.Id.</summary>
    public Guid? SelectedOptionId { get; set; }

    /// <summary>FillBlank: the raw text the candidate typed.</summary>
    public string? AnswerText { get; set; }

    public bool IsFlagged { get; set; }
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAtUtc { get; set; }
}
