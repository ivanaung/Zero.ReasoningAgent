using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public class NotificationDispatchBackgroundService(IServiceScopeFactory scopeFactory, ILogger<NotificationDispatchBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchDueNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification dispatch iteration failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
        }
    }

    private async Task DispatchDueNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queryService = scope.ServiceProvider.GetRequiredService<IRecommendationQueryService>();
        var composer = scope.ServiceProvider.GetRequiredService<IRecommendationComposer>();
        var aiSettingsService = scope.ServiceProvider.GetRequiredService<IAiSettingsService>();

        var dueEntries = await context.NotificationScheduleEntries
            .Where(item => item.Status == NotificationStatus.Pending && !item.IsDismissed && item.ScheduledForUtc <= DateTime.UtcNow.AddMinutes(1))
            .OrderBy(item => item.ScheduledForUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (dueEntries.Count == 0)
        {
            return;
        }

        var ai = await aiSettingsService.GetAsync(cancellationToken);
        foreach (var entry in dueEntries)
        {
            entry.Status = NotificationStatus.Processing;
            entry.UpdatedUtc = DateTime.UtcNow;
        }
        await context.SaveChangesAsync(cancellationToken);

        foreach (var entry in dueEntries)
        {
            try
            {
                var horizon = entry.NotificationType switch
                {
                    NotificationType.NextHour => RecommendationHorizon.NextHour,
                    NotificationType.TomorrowDigest => RecommendationHorizon.Tomorrow,
                    NotificationType.PreEventReminder => RecommendationHorizon.PreEventReminder,
                    _ => RecommendationHorizon.RiskFollowUp
                };

                var recommendationContext = await queryService.BuildContextAsync(new RecommendationRequest
                {
                    UserId = entry.UserId,
                    UserDisplayName = entry.UserDisplayName,
                    TimeZone = entry.TimeZone,
                    Horizon = horizon,
                    ReferenceTime = DateTimeOffset.UtcNow
                }, cancellationToken);

                entry.Message = await composer.ComposeNotificationMessageAsync(entry, recommendationContext, cancellationToken);
                entry.Status = NotificationStatus.Sent;
                entry.UpdatedUtc = DateTime.UtcNow;

                context.NotificationDeliveryLogs.Add(new NotificationDeliveryLog
                {
                    NotificationScheduleEntryId = entry.Id,
                    UserId = entry.UserId,
                    Channel = ai.SendInAppNotifications ? "InApp" : "Suppressed",
                    Status = "Sent",
                    ProviderModel = ai.EnableAiEnrichmentForNotifications ? $"{ai.ProviderType}/{ai.ModelId}" : "Deterministic"
                });
            }
            catch (Exception ex)
            {
                entry.RetryCount += 1;
                entry.Status = entry.RetryCount >= 3 ? NotificationStatus.Failed : NotificationStatus.Pending;
                entry.ScheduledForUtc = DateTime.UtcNow.AddMinutes(Math.Min(15, 2 * entry.RetryCount));
                entry.UpdatedUtc = DateTime.UtcNow;

                context.NotificationDeliveryLogs.Add(new NotificationDeliveryLog
                {
                    NotificationScheduleEntryId = entry.Id,
                    UserId = entry.UserId,
                    Channel = "InApp",
                    Status = "Failed",
                    ErrorMessage = ex.Message
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
