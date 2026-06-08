using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IRecommendationQueryService
{
    Task<RecommendationContextViewModel> BuildContextAsync(RecommendationRequest request, CancellationToken cancellationToken = default);
}

public class RecommendationQueryService(
    AppDbContext context,
    IAiSettingsService aiSettingsService,
    IUserProactivePreferenceService preferenceService) : IRecommendationQueryService
{
    public async Task<RecommendationContextViewModel> BuildContextAsync(RecommendationRequest request, CancellationToken cancellationToken = default)
    {
        var ai = await aiSettingsService.GetAsync(cancellationToken);
        var preference = await preferenceService.GetAsync(request.UserId, request.UserDisplayName, cancellationToken);
        var timeZone = SafeGetTimeZone(string.IsNullOrWhiteSpace(request.TimeZone) ? preference.TimeZone : request.TimeZone);
        var nowLocal = TimeZoneInfo.ConvertTime(request.ReferenceTime, timeZone);

        var startLocal = request.Horizon == RecommendationHorizon.Tomorrow
            ? nowLocal.Date.AddDays(1)
            : nowLocal.DateTime;
        var endLocal = request.Horizon switch
        {
            RecommendationHorizon.NextHour => nowLocal.DateTime.AddHours(1),
            RecommendationHorizon.Tomorrow => nowLocal.Date.AddDays(Math.Max(1, ai.DigestLookaheadDays + 1)),
            _ => nowLocal.DateTime.AddHours(Math.Max(2, ai.NotificationLookaheadHours))
        };

        var events = await context.Events
            .Include(evt => evt.Project)
            .Where(evt => !string.IsNullOrWhiteSpace(evt.AssignedTo)
                && (evt.AssignedTo == request.UserId || evt.AssignedTo == request.UserDisplayName))
            .OrderBy(evt => evt.StartDateTime)
            .ToListAsync(cancellationToken);

        var contextModel = new RecommendationContextViewModel
        {
            UserId = request.UserId,
            UserDisplayName = request.UserDisplayName,
            TimeZone = timeZone.Id,
            Horizon = request.Horizon,
            GeneratedAtUtc = request.ReferenceTime.UtcDateTime
        };

        contextModel.DueSoonTasks = events
            .Where(evt => evt.Status != EventStatus.Done && evt.EndDateTime >= startLocal && evt.EndDateTime <= endLocal)
            .Take(ai.MaxRecommendationsPerNotification)
            .Select(MapItem)
            .ToList();

        contextModel.OverdueTasks = events
            .Where(evt => evt.Status != EventStatus.Done && evt.EndDateTime < nowLocal.DateTime)
            .Take(ai.MaxRecommendationsPerNotification)
            .Select(MapItem)
            .ToList();

        contextModel.BlockedTasks = events
            .Where(evt => evt.Status == EventStatus.Blocked)
            .Take(ai.MaxRecommendationsPerNotification)
            .Select(MapItem)
            .ToList();

        contextModel.UpcomingMilestones = events
            .Where(evt => evt.Priority == EventPriority.Critical && evt.EndDateTime >= nowLocal.DateTime && evt.EndDateTime <= endLocal)
            .Take(ai.MaxRecommendationsPerNotification)
            .Select(MapItem)
            .ToList();

        contextModel.UpcomingAssignments = events
            .Where(evt => evt.Status != EventStatus.Done && evt.StartDateTime >= startLocal && evt.StartDateTime <= endLocal)
            .Take(ai.MaxRecommendationsPerNotification)
            .Select(MapItem)
            .ToList();

        contextModel.RiskItems = events
            .Where(evt => evt.Status == EventStatus.Blocked || (evt.Status != EventStatus.Done && evt.Priority >= EventPriority.High && evt.EndDateTime < endLocal))
            .Take(ai.MaxRecommendationsPerNotification)
            .Select(MapItem)
            .ToList();

        contextModel.CalendarSummary = $"Window {startLocal:yyyy-MM-dd HH:mm} to {endLocal:yyyy-MM-dd HH:mm}; assignments={contextModel.UpcomingAssignments.Count}; dueSoon={contextModel.DueSoonTasks.Count}; overdue={contextModel.OverdueTasks.Count}; blocked={contextModel.BlockedTasks.Count}; milestones={contextModel.UpcomingMilestones.Count}.";
        return contextModel;
    }

    private static RecommendationItemViewModel MapItem(ScheduleEvent evt)
    {
        return new RecommendationItemViewModel
        {
            TaskId = evt.Id,
            ProjectId = evt.ProjectId,
            Title = evt.Title,
            Detail = $"{evt.Project?.Name ?? "No project"} · {evt.StartDateTime:dd MMM HH:mm}-{evt.EndDateTime:HH:mm}",
            Status = evt.Status.ToString(),
            Priority = evt.Priority.ToString(),
            StartUtc = DateTime.SpecifyKind(evt.StartDateTime, DateTimeKind.Local),
            EndUtc = DateTime.SpecifyKind(evt.EndDateTime, DateTimeKind.Local),
            ActionUrl = $"/Events/Edit/{evt.Id}"
        };
    }

    private static TimeZoneInfo SafeGetTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}
