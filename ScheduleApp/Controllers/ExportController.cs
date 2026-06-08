using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

public class ExportController(IExportService exportService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Excel(int month, int year)
    {
        var data = await exportService.ExportEventsToExcelAsync(month, year);
        var fileName = $"Schedule_{year}_{month:D2}.xlsx";
        return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
