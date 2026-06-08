using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

[Authorize]
[Route("integrations/google")]
public class GoogleIntegrationController(IGoogleIntegrationService googleIntegrationService) : Controller
{
    [HttpPost("disconnect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        await googleIntegrationService.DisconnectAsync(cancellationToken);
        return RedirectToAction("Index", "Settings", new { tab = "integration", integrationSaved = true });
    }
}
