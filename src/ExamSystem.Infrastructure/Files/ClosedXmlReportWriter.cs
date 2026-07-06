using ClosedXML.Excel;
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.Reports.GetExamResultsReport;

namespace ExamSystem.Infrastructure.Files;

/// <summary>Renders admin reports to .xlsx with ClosedXML. Write-side counterpart of <see cref="ClosedXmlQuestionParser"/>.</summary>
public class ClosedXmlReportWriter : IExcelReportWriter
{
    private static readonly string[] ResultsHeaders =
    [
        "الاسم", "الرقم القومي", "رقم الجوال", "الدرجة", "النهاية العظمى",
        "النسبة %", "النتيجة", "وقت التسليم", "كود المحافظة", "مرات مغادرة الشاشة"
    ];

    public byte[] WriteExamResults(ExamResultsReportDto report)
    {
        using var workbook = new XLWorkbook();

        WriteSummarySheet(workbook, report);
        WriteResultsSheet(workbook, report);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSummarySheet(XLWorkbook workbook, ExamResultsReportDto report)
    {
        var sheet = workbook.Worksheets.Add("الملخص");
        sheet.RightToLeft = true;

        var pairs = new (string Label, XLCellValue Value)[]
        {
            ("الامتحان", report.ExamName),
            ("النهاية العظمى", report.TotalPoints),
            ("نسبة النجاح %", report.PassMarkPercentage),
            ("درجة النجاح", report.PassMarkPoints),
            ("إجمالي المتقدمين", report.Summary.TotalCandidates),
            ("عدد الناجحين", report.Summary.PassedCount),
            ("عدد الراسبين", report.Summary.FailedCount),
            ("نسبة النجاح الإجمالية %", report.Summary.PassRatePercentage)
        };

        for (var i = 0; i < pairs.Length; i++)
        {
            var row = i + 1;
            sheet.Cell(row, 1).Value = pairs[i].Label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = pairs[i].Value;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void WriteResultsSheet(XLWorkbook workbook, ExamResultsReportDto report)
    {
        var sheet = workbook.Worksheets.Add("النتائج");
        sheet.RightToLeft = true;

        for (var col = 0; col < ResultsHeaders.Length; col++)
        {
            var cell = sheet.Cell(1, col + 1);
            cell.Value = ResultsHeaders[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var rowIndex = 2;
        foreach (var row in report.Rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.FullName;
            sheet.Cell(rowIndex, 2).Value = row.NationalId;
            sheet.Cell(rowIndex, 3).Value = row.MobileNumber;
            sheet.Cell(rowIndex, 4).Value = row.Score;
            sheet.Cell(rowIndex, 5).Value = row.TotalPoints;
            sheet.Cell(rowIndex, 6).Value = row.ScorePercentage;
            sheet.Cell(rowIndex, 7).Value = row.Passed ? "ناجح" : "راسب";
            if (row.SubmittedAtUtc.HasValue)
            {
                sheet.Cell(rowIndex, 8).Value = row.SubmittedAtUtc.Value;
                sheet.Cell(rowIndex, 8).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            }
            sheet.Cell(rowIndex, 9).Value = row.GovernorateCode;
            sheet.Cell(rowIndex, 10).Value = row.TabSwitchCount;
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
    }
}
