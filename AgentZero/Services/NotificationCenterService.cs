using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface INotificationCenterService
{
    Task<List<NotificationCenterItemViewModel>> GetItemsAsync(string userId, CancellationToken cancellationToken = default);

    Task SnoozeAsync(int notificationId, string userId, int minutes, CancellationToken cancellationToken = default);

    Task DismissAsync(int notificationId, string userId, CancellationToken cancellationToken = default);
}

public class NotificationCenterService(AppDbContext context) : INotificationCenterService
{
    public async Task<List<NotificationCenterItemViewModel>> GetItemsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await context.NotificationScheduleEntries
            .Where(item => item.UserId == userId && !item.IsDismissed && item.Status != NotificationStatus.Cancelled)
            .OrderByDescending(item => item.ScheduledForUtc)
            .Take(20)
            .Select(item => new NotificationCenterItemViewModel
            {
                Id = item.Id,
                NotificationType = item.NotificationType,
                Title = item.NotificationType.ToString(),
                Message = item.Message,
                ScheduledForUtc = item.ScheduledForUtc,
                Status = item.Status,
                ActionUrl = item.ActionUrl,
                TaskId = item.TaskId,
                ProjectId = item.ProjectId
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SnoozeAsync(int notificationId, string userId, int minutes, CancellationToken cancellationToken = default)
    {
        var entry = await context.NotificationScheduleEntries.FirstOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId, cancellationToken);
        if (entry == null)
        {
            return;
        }

        entry.Status = NotificationStatus.Pending;
        entry.ScheduledForUtc = DateTime.UtcNow.AddMinutes(Math.Max(5, minutes));
        entry.UpdatedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DismissAsync(int notificationId, string userId, CancellationToken cancellationToken = default)
    {
        var entry = await context.NotificationScheduleEntries.FirstOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId, cancellationToken);
        if (entry == null)
        {
            return;
        }

        entry.IsDismissed = true;
        entry.Status = NotificationStatus.Cancelled;
        entry.UpdatedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }
}
