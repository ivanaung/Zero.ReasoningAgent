using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

public class ZeroAssistantController(
    IZeroAssistantService zeroAssistantService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await zeroAssistantService.GetPageModelAsync(cancellationToken));
    }
}
