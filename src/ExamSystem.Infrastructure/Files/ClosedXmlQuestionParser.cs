using ClosedXML.Excel;
using ExamSystem.Application.Common.Interfaces;

namespace ExamSystem.Infrastructure.Files;

public class ClosedXmlQuestionParser : IExcelQuestionParser
{
    public ParsedQuestionWorkbook Parse(Stream fileContent)
    {
        using var workbook = new XLWorkbook(fileContent);
        var mcqRows = new List<ParsedMcqRow>();
        var fillBlankRows = new List<ParsedFillBlankRow>();

        if (workbook.Worksheets.TryGetWorksheet("MCQ", out var mcqSheet))
        {
            var usedRange = mcqSheet.RangeUsed();
            if (usedRange is not null)
            {
                foreach (var row in usedRange.RowsUsed().Skip(1))
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
        }

        if (workbook.Worksheets.TryGetWorksheet("FillBlank", out var fillBlankSheet))
        {
            var usedRange = fillBlankSheet.RangeUsed();
            if (usedRange is not null)
            {
                foreach (var row in usedRange.RowsUsed().Skip(1))
                {
                    fillBlankRows.Add(new ParsedFillBlankRow(
                        row.RowNumber(),
                        row.Cell(1).GetString().Trim(),
                        row.Cell(3).GetString().Trim(),
                        row.Cell(4).GetString().Trim(),
                        row.Cell(5).GetString().Trim()));
                }
            }
        }

        return new ParsedQuestionWorkbook(mcqRows, fillBlankRows);
    }
}
