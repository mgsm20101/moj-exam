namespace ExamSystem.Application.Features.CandidateExam.GetExamLanding;

public record ExamLandingDto(
    Guid ExamId,
    string Name,
    string? Description,
    bool IsOpen,
    int DurationMinutes,
    int TotalQuestionCount);
