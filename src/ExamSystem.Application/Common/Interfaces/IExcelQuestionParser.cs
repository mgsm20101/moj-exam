namespace ExamSystem.Application.Common.Interfaces;

public record ParsedMcqRow(int RowNumber, string Topic, string Difficulty, string QuestionText, string OptionA, string OptionB, string OptionC, string OptionD, string CorrectOption);

public record ParsedFillBlankRow(int RowNumber, string Topic, string Difficulty, string QuestionText, string CorrectAnswer);

public record ParsedQuestionWorkbook(List<ParsedMcqRow> McqRows, List<ParsedFillBlankRow> FillBlankRows);

public interface IExcelQuestionParser
{
    ParsedQuestionWorkbook Parse(Stream fileContent);
}
