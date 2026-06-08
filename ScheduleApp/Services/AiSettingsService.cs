using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IAiSettingsService
{
    Task<AiSettings> GetAsync(CancellationToken cancellationToken = default);

    Task<AiSettingsInputViewModel> GetInputModelAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AiSettingsInputViewModel model, CancellationToken cancellationToken = default);
}

public class AiSettingsService(
    AppDbContext context,
    IDataProtectionProvider dataProtectionProvider) : IAiSettingsService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("ScheduleApp.AI.Settings.ApiKey");

    public async Task<AiSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await context.AiSettings.FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            entity = new AiSettings();
            context.AiSettings.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
        }

        entity.ApiKey = Decrypt(entity.ApiKeyEncrypted);
        return entity;
    }

    public async Task<AiSettingsInputViewModel> GetInputModelAsync(CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync(cancellationToken);
        return new AiSettingsInputViewModel
        {
            IsEnabled = entity.IsEnabled,
            ProviderType = entity.ProviderType,
            ModelId = entity.ModelId,
            EndpointUrl = entity.EndpointUrl,
            Temperature = entity.Temperature,
            MaxTokens = entity.MaxTokens,
            SystemPrompt = entity.SystemPrompt,
            EnableToolCalling = entity.EnableToolCalling,
            EnableStreaming = entity.EnableStreaming,
            EnableProgressMonitoring = entity.EnableProgressMonitoring,
            DefaultTimeZone = entity.DefaultTimeZone,
            WorkingHoursStart = entity.WorkingHoursStart,
            WorkingHoursEnd = entity.WorkingHoursEnd,
            WorkingDays = entity.WorkingDays,
            AutoCreateLowRiskTasks = entity.AutoCreateLowRiskTasks,
            RequireApprovalForScheduleChange = entity.RequireApprovalForScheduleChange,
            RequireApprovalForTaskDeletion = entity.RequireApprovalForTaskDeletion,
            EnableProactiveAssist = entity.EnableProactiveAssist,
            EnableNextHourRecommendations = entity.EnableNextHourRecommendations,
            EnableTomorrowRecommendations = entity.EnableTomorrowRecommendations,
            EnablePreEventReminders = entity.EnablePreEventReminders,
            PreEventReminderMinutes = entity.PreEventReminderMinutes,
            MorningDigestTime = entity.MorningDigestTime,
            AfternoonDigestTime = entity.AfternoonDigestTime,
            MaxRecommendationsPerNotification = entity.MaxRecommendationsPerNotification,
            QuietHoursStart = entity.QuietHoursStart,
            QuietHoursEnd = entity.QuietHoursEnd,
            SendInAppNotifications = entity.SendInAppNotifications,
            SendPushNotifications = entity.SendPushNotifications,
            SendEmailNotifications = entity.SendEmailNotifications,
            EnableAiEnrichmentForNotifications = entity.EnableAiEnrichmentForNotifications,
            NotificationLookaheadHours = entity.NotificationLookaheadHours,
            DigestLookaheadDays = entity.DigestLookaheadDays,
            RecomputeOnTaskUpdate = entity.RecomputeOnTaskUpdate,
            RecomputeOnAssignmentChange = entity.RecomputeOnAssignmentChange,
            RecomputeOnDependencyChange = entity.RecomputeOnDependencyChange,
            RequireUserOptInForProactiveAssist = entity.RequireUserOptInForProactiveAssist,
            DefaultUserTimeZone = entity.DefaultUserTimeZone,
            ApiKey = string.IsNullOrWhiteSpace(entity.ApiKey) ? null : "configured"
        };
    }

    public async Task SaveAsync(AiSettingsInputViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await context.AiSettings.FirstOrDefaultAsync(cancellationToken) ?? new AiSettings();
        if (entity.Id == 0)
        {
            entity.Id = 1;
            context.AiSettings.Add(entity);
        }

        entity.IsEnabled = model.IsEnabled;
        entity.ProviderType = model.ProviderType;
        entity.ModelId = model.ModelId.Trim();
        entity.EndpointUrl = model.EndpointUrl.Trim();
        entity.Temperature = model.Temperature;
        entity.MaxTokens = model.MaxTokens;
        entity.SystemPrompt = model.SystemPrompt.Trim();
        entity.EnableToolCalling = model.EnableToolCalling;
        entity.EnableStreaming = model.EnableStreaming;
        entity.EnableProgressMonitoring = model.EnableProgressMonitoring;
        entity.DefaultTimeZone = model.DefaultTimeZone.Trim();
        entity.WorkingHoursStart = model.WorkingHoursStart;
        entity.WorkingHoursEnd = model.WorkingHoursEnd;
        entity.WorkingDays = model.WorkingDays.Trim();
        entity.AutoCreateLowRiskTasks = model.AutoCreateLowRiskTasks;
        entity.RequireApprovalForScheduleChange = model.RequireApprovalForScheduleChange;
        entity.RequireApprovalForTaskDeletion = model.RequireApprovalForTaskDeletion;
        entity.EnableProactiveAssist = model.EnableProactiveAssist;
        entity.EnableNextHourRecommendations = model.EnableNextHourRecommendations;
        entity.EnableTomorrowRecommendations = model.EnableTomorrowRecommendations;
        entity.EnablePreEventReminders = model.EnablePreEventReminders;
        entity.PreEventReminderMinutes = model.PreEventReminderMinutes;
        entity.MorningDigestTime = model.MorningDigestTime;
        entity.AfternoonDigestTime = model.AfternoonDigestTime;
        entity.MaxRecommendationsPerNotification = model.MaxRecommendationsPerNotification;
        entity.QuietHoursStart = model.QuietHoursStart;
        entity.QuietHoursEnd = model.QuietHoursEnd;
        entity.SendInAppNotifications = model.SendInAppNotifications;
        entity.SendPushNotifications = model.SendPushNotifications;
        entity.SendEmailNotifications = model.SendEmailNotifications;
        entity.EnableAiEnrichmentForNotifications = model.EnableAiEnrichmentForNotifications;
        entity.NotificationLookaheadHours = model.NotificationLookaheadHours;
        entity.DigestLookaheadDays = model.DigestLookaheadDays;
        entity.RecomputeOnTaskUpdate = model.RecomputeOnTaskUpdate;
        entity.RecomputeOnAssignmentChange = model.RecomputeOnAssignmentChange;
        entity.RecomputeOnDependencyChange = model.RecomputeOnDependencyChange;
        entity.RequireUserOptInForProactiveAssist = model.RequireUserOptInForProactiveAssist;
        entity.DefaultUserTimeZone = model.DefaultUserTimeZone.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (model.ClearApiKey)
        {
            entity.ApiKeyEncrypted = null;
        }
        else if (!string.IsNullOrWhiteSpace(model.ApiKey) && !string.Equals(model.ApiKey, "configured", StringComparison.Ordinal))
        {
            entity.ApiKeyEncrypted = _protector.Protect(model.ApiKey.Trim());
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private string? Decrypt(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(encrypted);
        }
        catch
        {
            return null;
        }
    }
}
