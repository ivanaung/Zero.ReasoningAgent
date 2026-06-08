using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Models.ViewModels;
using ScheduleApp.Services;
using System.Net;

namespace ScheduleApp.Controllers;

public class EmailHubController(IGoogleIntegrationService googleIntegrationService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new EmailHubViewModel
        {
            Status = await googleIntegrationService.GetStatusAsync(cancellationToken)
        };
        model.InboxDisplayLimit = model.Status.InboxCacheLimit;

        try
        {
            model.InboxMessages = await googleIntegrationService.GetCachedInboxMessagesAsync(model.InboxDisplayLimit, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            model.InboxError = BuildGoogleAccessErrorMessage("Gmail", ex);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Feed([FromQuery] int take = 12, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await googleIntegrationService.GetStatusAsync(cancellationToken);
            var items = await googleIntegrationService.GetInboxMessagesAsync(
                Math.Clamp(take, 1, Math.Clamp(status.InboxCacheLimit, 10, 500)),
                cancellationToken: cancellationToken);
            return Json(new
            {
                ok = true,
                items = items.Select(item => new
                {
                    id = item.Id,
                    subject = item.Subject,
                    from = item.From,
                    receivedAt = item.ReceivedAt?.ToString("o"),
                    snippet = item.Snippet,
                    webUrl = item.WebUrl,
                    htmlBody = item.HtmlBody,
                    textBody = item.TextBody
                })
            });
        }
        catch (HttpRequestException ex)
        {
            return Json(new
            {
                ok = false,
                error = BuildGoogleAccessErrorMessage("Gmail", ex)
            });
        }
    }

    private static string BuildGoogleAccessErrorMessage(string providerName, HttpRequestException exception)
    {
        if (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return $"{providerName} access was denied by Google. This usually means the account was linked without the required API scope, the API is not enabled in Google Cloud, or the Google app is still restricted. Disconnect the Google account and sign in again after confirming the required API permissions.";
        }

        if (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            return $"{providerName} authorization has expired or is invalid. Disconnect the Google account and sign in again.";
        }

        return $"Unable to load {providerName} data right now: {exception.Message}";
    }
}
