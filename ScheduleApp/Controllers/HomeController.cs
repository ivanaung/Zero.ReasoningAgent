using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Models.ViewModels;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

public class HomeController(ISettingsService settingsService, IDashboardService dashboardService) : Controller
{
    public async Task<IActionResult> Index(int? projectId)
    {
        var settings = await settingsService.GetSettingsAsync();
        ViewBag.StartTime = settings.DayStartTime;
        ViewBag.EndTime = settings.DayEndTime;
        ViewBag.ShowFullDay = settings.ShowFullDayInCalendar;

        var dashboard = await dashboardService.GetDashboardAsync(projectId);
        return View(dashboard);
    }

    public IActionResult Error()
    {
        return View();
    }
}
