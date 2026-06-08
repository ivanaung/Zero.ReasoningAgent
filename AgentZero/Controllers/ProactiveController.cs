using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

[Route("proactive")]
public class ProactiveController(
    IAiSettingsService aiSettingsService,
    IUserProactivePreferenceService preferenceService,
    IRecommendationQueryService recommendationQueryService,
    IRecommendationComposer recommendationComposer,
    INotificationCenterService notificationCenterService,
    INotificationPlannerService notificationPlannerService,
    ICurrentUserService currentUserService,
    IAiAuditService aiAuditService) : Controller
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        return Json(await aiSettingsService.GetInputModelAsync(cancellationToken));
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        return Json(await preferenceService.GetInputAsync(currentUserService.UserId, currentUserService.DisplayName, cancellationToken));
    }

    [HttpPost("preferences")]
    public async Task<IActionResult> SavePreferences([FromBody] UserProactivePreferenceInputViewModel model, CancellationToken cancellationToken)
    {
        model.UserId = currentUserService.UserId;
        model.UserDisplayName = currentUserService.DisplayName;
        await preferenceService.SaveAsync(model, cancellationToken);
        return Json(new { success = true });
    }

    [HttpGet("recommendations/next-hour")]
    public async Task<IActionResult> GetNextHour(CancellationToken cancellationToken)
    {
        return Json(await BuildRecommendationAsync(RecommendationHorizon.NextHour, cancellationToken));
    }

    [HttpGet("recommendations/tomorrow")]
    public async Task<IActionResult> GetTomorrow(CancellationToken cancellationToken)
    {
        return Json(await BuildRecommendationAsync(RecommendationHorizon.Tomorrow, cancellationToken));
    }

    [HttpGet("recommendations/attention")]
    public async Task<IActionResult> GetAttention(CancellationToken cancellationToken)
    {
        return Json(await BuildRecommendationAsync(RecommendationHorizon.NeedsAttention, cancellationToken));
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(CancellationToken cancellationToken)
    {
        var items = await notificationCenterService.GetItemsAsync(currentUserService.UserId, cancellationToken);
        return Json(items.Select(item => new
        {
            item.Id,
            NotificationType = item.NotificationType.ToString(),
            item.Title,
            item.Message,
            item.ScheduledForUtc,
            Status = item.Status.ToString(),
            ActionUrl = ResolveActionUrl(item.ActionUrl),
            item.TaskId,
            item.ProjectId
        }));
    }

    [HttpPost("notifications/{notificationId:int}/snooze")]
    public async Task<IActionResult> Snooze(int notificationId, [FromQuery] int minutes = 15, CancellationToken cancellationToken = default)
    {
        await notificationCenterService.SnoozeAsync(notificationId, currentUserService.UserId, minutes, cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("notifications/{notificationId:int}/dismiss")]
    public async Task<IActionResult> Dismiss(int notificationId, CancellationToken cancellationToken = default)
    {
        await notificationCenterService.DismissAsync(notificationId, currentUserService.UserId, cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("recompute")]
    public async Task<IActionResult> ManualRecompute([FromQuery] int? taskId = null, CancellationToken cancellationToken = default)
    {
        if (taskId.HasValue)
        {
            await notificationPlannerService.RecomputeNotificationPlanForTaskAsync(taskId.Value, "Manual", cancellationToken);
        }

        return Json(new { success = true });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(CancellationToken cancellationToken)
    {
        var items = await aiAuditService.GetRecentAsync(50, cancellationToken);
        return Json(items
            .Where(item => item.ActionType.StartsWith("notification.", StringComparison.OrdinalIgnoreCase) || item.ActionType.StartsWith("recommendation.", StringComparison.OrdinalIgnoreCase))
            .Select(item => new
            {
                item.Id,
                item.ActionType,
                item.Summary,
                item.Outcome,
                item.OccurredAtUtc
            }));
    }

    private async Task<RecommendationResultViewModel> BuildRecommendationAsync(RecommendationHorizon horizon, CancellationToken cancellationToken)
    {
        var preference = await preferenceService.GetInputAsync(currentUserService.UserId, currentUserService.DisplayName, cancellationToken);
        var context = await recommendationQueryService.BuildContextAsync(new RecommendationRequest
        {
            UserId = currentUserService.UserId,
            UserDisplayName = currentUserService.DisplayName,
            TimeZone = preference.TimeZone,
            Horizon = horizon,
            ReferenceTime = DateTimeOffset.UtcNow
        }, cancellationToken);

        var ai = await aiSettingsService.GetAsync(cancellationToken);
        var result = await recommendationComposer.ComposeAsync(context, ai.EnableAiEnrichmentForNotifications, cancellationToken);
        await aiAuditService.LogAsync("recommendation.query", $"Built {horizon} recommendation.", "Succeeded", horizon.ToString(), result.Summary, cancellationToken: cancellationToken);
        return result;
    }

    private string? ResolveActionUrl(string? actionUrl)
    {
        if (string.IsNullOrWhiteSpace(actionUrl))
        {
            return null;
        }

        if (Uri.TryCreate(actionUrl, UriKind.Absolute, out _))
        {
            return actionUrl;
        }

        if (actionUrl == "/")
        {
            return Url.Action("Index", "Home");
        }

        return Url.Content($"~{(actionUrl.StartsWith('/') ? actionUrl : $"/{actionUrl}")}");
    }
}
