using ClosedXML.Excel;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IExportService
{
    Task<byte[]> ExportEventsToExcelAsync(int month, int year);
}

public class ExcelExportService(IEventService eventService) : IExportService
{
    public async Task<byte[]> ExportEventsToExcelAsync(int month, int year)
    {
        var startOfMonth = new DateTime(year, month, 1);
        var endOfMonth = startOfMonth.AddMonths(1);
        
        var events = await eventService.GetEventsInRangeAsync(startOfMonth, endOfMonth);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Schedule");

        // Headers
        var headers = new[] { "#", "Product", "Title", "Progress (%)", "Status", "Date", "Start", "End", "Category", "Priority", "Description" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E5DA6");
            cell.Style.Font.FontColor = XLColor.White;
        }

        // Data
        int row = 2;
        foreach (var evt in events)
        {
            worksheet.Cell(row, 1).Value = evt.Id;
            worksheet.Cell(row, 2).Value = evt.Project?.Name ?? "No Product";
            worksheet.Cell(row, 3).Value = evt.Title;
            worksheet.Cell(row, 4).Value = evt.Progress;
            worksheet.Cell(row, 5).Value = evt.Status.ToString();
            worksheet.Cell(row, 6).Value = evt.StartDateTime.ToShortDateString();
            worksheet.Cell(row, 7).Value = evt.StartDateTime.ToShortTimeString();
            worksheet.Cell(row, 8).Value = evt.EndDateTime.ToShortTimeString();
            worksheet.Cell(row, 9).Value = evt.Category?.Name ?? "";
            worksheet.Cell(row, 10).Value = evt.Priority.ToString();
            worksheet.Cell(row, 11).Value = evt.Description ?? "";
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
