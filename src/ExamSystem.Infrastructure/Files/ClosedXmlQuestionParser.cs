using ClosedXML.Excel;
using ExamSystem.Application.Common.Interfaces;

namespace ExamSystem.Infrastructure.Files;

public class ClosedXmlQuestionParser : IExcelQuestionParser
{
    private static readonly string[] McqExpectedHeaders =
        ["Topic", "Type", "Difficulty", "QuestionText", "OptionA", "OptionB", "OptionC", "OptionD", "CorrectOption"];

    private static readonly string[] FillBlankExpectedHeaders =
        ["Topic", "Type", "Difficulty", "QuestionText", "CorrectAnswer"];

    public ParsedQuestionWorkbook Parse(Stream fileContent)
    {
        using var workbook = new XLWorkbook(fileContent);
        var mcqRows = new List<ParsedMcqRow>();
        var fillBlankRows = new List<ParsedFillBlankRow>();

        if (!workbook.Worksheets.TryGetWorksheet("MCQ", out var mcqSheet))
        {
            throw new InvalidDataException("Missing required worksheet 'MCQ'.");
        }
        ValidateHeaderRow(mcqSheet, "MCQ", McqExpectedHeaders);

        var mcqUsedRange = mcqSheet.RangeUsed();
        if (mcqUsedRange is not null)
        {
            foreach (var row in mcqUsedRange.RowsUsed().Skip(1))
            {
                mcqRows.Add(new ParsedMcqRow(
                    row.RowNumber(),
                    row.Cell(1).GetString().Trim(),
                    row.Cell(3).GetString().Trim(),
                    row.Cell(4).GetString().Trim(),
                    row.Cell(5).GetString().Trim(),
                    row.Cell(6).GetString().Trim(),
                    row.Cell(7).GetString().Trim(),
                    row.Cell(8).GetString().Trim(),
                    row.Cell(9).GetString().Trim()));
            }
        }

        if (!workbook.Worksheets.TryGetWorksheet("FillBlank", out var fillBlankSheet))
        {
            throw new InvalidDataException("Missing required worksheet 'FillBlank'.");
        }
        ValidateHeaderRow(fillBlankSheet, "FillBlank", FillBlankExpectedHeaders);

        var fillBlankUsedRange = fillBlankSheet.RangeUsed();
        if (fillBlankUsedRange is not null)
        {
            foreach (var row in fillBlankUsedRange.RowsUsed().Skip(1))
            {
                fillBlankRows.Add(new ParsedFillBlankRow(
                    row.RowNumber(),
                    row.Cell(1).GetString().Trim(),
                    row.Cell(3).GetString().Trim(),
                    row.Cell(4).GetString().Trim(),
                    row.Cell(5).GetString().Trim()));
            }
        }

        return new ParsedQuestionWorkbook(mcqRows, fillBlankRows);
    }

    private static void ValidateHeaderRow(IXLWorksheet sheet, string sheetName, string[] expectedHeaders)
    {
        var headerRow = sheet.Row(1);
        var actualHeaders = expectedHeaders
            .Select((_, index) => headerRow.Cell(index + 1).GetString().Trim())
            .ToArray();

        if (!actualHeaders.SequenceEqual(expectedHeaders, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"Worksheet '{sheetName}' has an unexpected header row. " +
                $"Expected columns [{string.Join(", ", expectedHeaders)}] but found [{string.Join(", ", actualHeaders)}].");
        }
    }
}
