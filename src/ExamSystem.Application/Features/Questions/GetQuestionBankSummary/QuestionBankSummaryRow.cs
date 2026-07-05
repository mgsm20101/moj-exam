using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Questions.GetQuestionBankSummary;

public record QuestionBankSummaryRow(string TopicName, DifficultyLevel Difficulty, int McqCount, int FillBlankCount);
