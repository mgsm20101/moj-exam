using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Common.Models;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;

namespace ExamSystem.Infrastructure.Grading;

public class AttemptGradingService : IAttemptGradingService
{
    public GradeResult Grade(ExamAttempt attempt, Exam exam)
    {
        var answersByQuestion = attempt.Answers.ToDictionary(a => a.AttemptQuestionId);
        decimal score = 0m;
        decimal total = 0m;

        foreach (var question in attempt.Questions)
        {
            var points = PointsFor(question.Type, exam);
            total += points;

            if (!answersByQuestion.TryGetValue(question.Id, out var answer))
            {
                continue; // unanswered -> wrong, contributes 0
            }

            var correct = question.Type == QuestionType.FillBlank
                ? FillBlankAnswerRules.Normalize(answer.AnswerText) == (question.CorrectAnswerTextSnapshot ?? string.Empty)
                : answer.SelectedOptionId is { } optionId
                  && question.Options.Any(o => o.Id == optionId && o.IsCorrect);

            answer.IsCorrect = correct;
            if (correct)
            {
                score += points;
            }
        }

        var passed = total > 0m && score / total * 100m >= exam.PassMarkPercentage;
        return new GradeResult(score, total, exam.PassMarkPercentage, passed);
    }

    private static decimal PointsFor(QuestionType type, Exam exam) => type switch
    {
        QuestionType.Mcq => exam.McqPoints,
        QuestionType.TrueFalse => exam.TrueFalsePoints,
        QuestionType.FillBlank => exam.FillBlankPoints,
        _ => 0m
    };
}
