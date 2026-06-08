using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface INotificationPlannerService
{
    Task RecomputeNotificationPlanForTaskAsync(int taskId, string triggerSource, CancellationToken cancellationToken = default);

    Task CancelNotificationScheduleEntriesForTaskAsync(int taskId, string triggerSource, CancellationToken cancellationToken = default);

    Task<int> CreateNotificationScheduleEntryAsync(NotificationScheduleEntry entry, CancellationToken cancellationToken = default);
}

public class NotificationPlannerService(
    AppDbContext context,
    IAiSettingsService aiSettingsService,
    IUserProactivePreferenceService preferenceService,
    IAiAuditService auditService) : INotificationPlannerService
{
    public async Task RecomputeNotificationPlanForTaskAsync(int taskId, string triggerSource, CancellationToken cancellationToken = default)
    {
        var task = await context.Events
            .Include(evt => evt.Project)
            .FirstOrDefaultAsync(evt => evt.Id == taskId, cancellationToken);
        if (task == null)
        {
            return;
        }

        await CancelNotificationScheduleEntriesForTaskAsync(taskId, triggerSource, cancellationToken);

        if (string.IsNullOrWhiteSpace(task.AssignedTo))
        {
            return;
        }

        var ai = await aiSettingsService.GetAsync(cancellationToken);
        if (!ai.EnableProactiveAssist)
        {
            return;
        }

        var userId = task.AssignedTo.Trim();
        var preference = await preferenceService.GetAsync(userId, userId, cancellationToken);
        if (!IsUserEligible(ai, preference))
        {
            return;
        }

        if (ai.EnablePreEventReminders && preference.PreEventReminderEnabled && task.Status != EventStatus.Done)
        {
            var reminderUtc = ToUtc(task.StartDateTime, preference.TimeZone).AddMinutes(-preference.ReminderMinutesBefore);
            if (reminderUtc > DateTime.UtcNow.AddMinutes(-5))
            {
                await UpsertAsync(new NotificationScheduleEntry
                {
                    UserId = preference.UserId,
                    UserDisplayName = preference.UserDisplayName,
                    ProjectId = task.ProjectId,
                    TaskId = task.Id,
                    NotificationType = NotificationType.PreEventReminder,
                    ScheduledForUtc = reminderUtc,
                    TimeZone = preference.TimeZone,
                    Priority = task.Priority >= EventPriority.High ? 1 : 2,
                    TriggerSource = triggerSource,
                    DeduplicationKey = $"pre:{task.Id}:{reminderUtc:yyyyMMddHHmm}",
                    Message = $"{task.Title} starts in {preference.ReminderMinutesBefore} minutes.",
                    ActionUrl = $"/Events/Edit/{task.Id}",
                    ContextSnapshotJson = JsonSerializer.Serialize(new { task.Id, task.Title, task.StartDateTime, task.EndDateTime, Project = task.Project?.Name })
                }, cancellationToken);
            }
        }

        if (preference.TomorrowDigestEnabled && ai.EnableTomorrowRecommendations)
        {
            var digestLocal = ParseTime(preference.PreferredAfternoonDigestTime);
            var nextDigestLocal = DateTime.Today.Add(digestLocal);
            if (nextDigestLocal < DateTime.Now)
            {
                nextDigestLocal = nextDigestLocal.AddDays(1);
            }

            await UpsertAsync(new NotificationScheduleEntry
            {
                UserId = preference.UserId,
                UserDisplayName = preference.UserDisplayName,
                ProjectId = task.ProjectId,
                NotificationType = NotificationType.TomorrowDigest,
                ScheduledForUtc = ToUtc(nextDigestLocal, preference.TimeZone),
                TimeZone = preference.TimeZone,
                Priority = 3,
                TriggerSource = triggerSource,
                DeduplicationKey = $"digest:{preference.UserId}:{nextDigestLocal:yyyyMMdd}",
                Message = "Tomorrow digest is ready.",
                ActionUrl = "/",
                ContextSnapshotJson = JsonSerializer.Serialize(new { UserId = preference.UserId, Date = nextDigestLocal.Date })
            }, cancellationToken);
        }

        if (task.Status == EventStatus.Blocked || task.IsOverdue || task.Priority == EventPriority.Critical)
        {
            var triggerReason = task.Status == EventStatus.Blocked ? "blocked" : (task.IsOverdue ? "overdue" : "marked CRITICAL priority");
            await UpsertAsync(new NotificationScheduleEntry
            {
                UserId = preference.UserId,
                UserDisplayName = preference.UserDisplayName,
                ProjectId = task.ProjectId,
                TaskId = task.Id,
                NotificationType = NotificationType.RiskAlert,
                ScheduledForUtc = DateTime.UtcNow.AddMinutes(2),
                TimeZone = preference.TimeZone,
                Priority = 1,
                TriggerSource = triggerSource,
                DeduplicationKey = $"risk:{task.Id}:{task.Status}:{task.Priority}:{task.EndDateTime:yyyyMMddHHmm}",
                Message = $"{task.Title} needs attention because it is {triggerReason}.",
                ActionUrl = $"/Events/Edit/{task.Id}",
                ContextSnapshotJson = JsonSerializer.Serialize(new { task.Id, task.Title, task.Status, task.Priority, task.EndDateTime })
            }, cancellationToken);
        }
    }

    public async Task CancelNotificationScheduleEntriesForTaskAsync(int taskId, string triggerSource, CancellationToken cancellationToken = default)
    {
        var entries = await context.NotificationScheduleEntries
            .Where(item => item.TaskId == taskId && (item.Status == NotificationStatus.Pending || item.Status == NotificationStatus.Snoozed))
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            return;
        }

        foreach (var entry in entries)
        {
            entry.Status = NotificationStatus.Cancelled;
            entry.UpdatedUtc = DateTime.UtcNow;
            entry.TriggerSource = triggerSource;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CreateNotificationScheduleEntryAsync(NotificationScheduleEntry entry, CancellationToken cancellationToken = default)
    {
        await UpsertAsync(entry, cancellationToken);
        return entry.Id;
    }

    private async Task UpsertAsync(NotificationScheduleEntry candidate, CancellationToken cancellationToken)
    {
        var existing = await context.NotificationScheduleEntries
            .FirstOrDefaultAsync(item => item.DeduplicationKey == candidate.DeduplicationKey && item.Status != NotificationStatus.Sent && item.Status != NotificationStatus.Cancelled, cancellationToken);

        if (existing == null)
        {
            context.NotificationScheduleEntries.Add(candidate);
        }
        else
        {
            existing.ScheduledForUtc = candidate.ScheduledForUtc;
            existing.TimeZone = candidate.TimeZone;
            existing.Priority = candidate.Priority;
            existing.ContextSnapshotJson = candidate.ContextSnapshotJson;
            existing.LastComputedUtc = DateTime.UtcNow;
            existing.TriggerSource = candidate.TriggerSource;
            existing.Message = candidate.Message;
            existing.ActionUrl = candidate.ActionUrl;
            existing.Status = NotificationStatus.Pending;
            existing.IsDismissed = false;
            existing.UpdatedUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync("notification.plan", $"Planned notification {candidate.DeduplicationKey}.", "Succeeded", candidate.TriggerSource, candidate.Message, cancellationToken: cancellationToken);
    }

    private static bool IsUserEligible(AiSettings ai, UserProactivePreference preference)
    {
        return !ai.RequireUserOptInForProactiveAssist || preference.IsOptedIn;
    }

    private static TimeSpan ParseTime(string value) => TimeSpan.Parse(value);

    private static DateTime ToUtc(DateTime localDateTime, string timeZoneId)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), timeZone);
        }
        catch
        {
            return DateTime.SpecifyKind(localDateTime, DateTimeKind.Local).ToUniversalTime();
        }
    }
}
