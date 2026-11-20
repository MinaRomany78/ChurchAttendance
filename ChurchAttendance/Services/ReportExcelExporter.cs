using ChurchAttendance.ViewModels;
using ClosedXML.Excel;

namespace ChurchAttendance.Services;

/// <summary>Turns any ReportResult into a right-to-left Excel workbook.</summary>
public static class ReportExcelExporter
{
    public static byte[] Export(ReportResult report, string churchName)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(SheetName(report.Title));
        ws.RightToLeft = true;

        var columnCount = Math.Max(report.Columns.Count, 2);
        var row = 1;

        ws.Cell(row, 1).Value = churchName;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 12;
        row++;

        ws.Cell(row, 1).Value = report.Title;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 15;
        row++;

        ws.Cell(row, 1).Value = report.FiltersText;
        row++;

        ws.Cell(row, 1).Value = "تاريخ الإصدار: " + report.GeneratedAtLocal.ToString("yyyy/MM/dd hh:mm tt");
        row += 2;

        foreach (var item in report.Summary)
        {
            ws.Cell(row, 1).Value = item.Label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = item.Value;
            row++;
        }

        if (report.Summary.Count > 0) row++;

        // ---- header
        var headerRow = row;
        for (var c = 0; c < report.Columns.Count; c++)
        {
            ws.Cell(headerRow, c + 1).Value = report.Columns[c].Header;
        }

        var header = ws.Range(headerRow, 1, headerRow, columnCount);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#12356B");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row++;

        // ---- data
        foreach (var data in report.Rows)
        {
            for (var c = 0; c < report.Columns.Count; c++)
            {
                var cell = ws.Cell(row, c + 1);
                var value = c < data.Length ? data[c] : null;
                if (value is null) continue;

                switch (report.Columns[c].Kind)
                {
                    case ReportColumnKind.Number:
                        cell.Value = Convert.ToDouble(value);
                        cell.Style.NumberFormat.Format = "#,##0";
                        break;
                    case ReportColumnKind.Percent:
                        cell.Value = Convert.ToDouble(value);
                        cell.Style.NumberFormat.Format = "0.0\"%\"";
                        break;
                    case ReportColumnKind.Date when value is DateTime d:
                        cell.Value = d;
                        cell.Style.NumberFormat.Format = "yyyy/mm/dd";
                        break;
                    case ReportColumnKind.DateTime when value is DateTime dt:
                        cell.Value = dt;
                        cell.Style.NumberFormat.Format = "yyyy/mm/dd hh:mm";
                        break;
                    default:
                        cell.Value = Convert.ToString(value) ?? string.Empty;
                        break;
                }
            }

            row++;
        }

        if (report.Rows.Count == 0)
        {
            ws.Cell(row, 1).Value = "لا توجد بيانات ضمن الفلاتر المحددة.";
            row++;
        }

        if (report.Truncated)
        {
            ws.Cell(row + 1, 1).Value = $"تنبيه: تم عرض أول {report.MaxRows:N0} سجل فقط.";
        }

        ws.SheetView.FreezeRows(headerRow);

        ws.Columns().AdjustToContents();
        foreach (var column in ws.ColumnsUsed())
        {
            if (column.Width > 45) column.Width = 45;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string SheetName(string title)
    {
        var invalid = new[] { '[', ']', ':', '*', '?', '/', '\\' };
        var clean = new string(title.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
        if (clean.Length == 0) clean = "Report";
        return clean.Length > 31 ? clean[..31] : clean;
    }
}
