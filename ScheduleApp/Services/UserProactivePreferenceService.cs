using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IUserProactivePreferenceService
{
    Task<UserProactivePreference> GetAsync(string userId, string userDisplayName, CancellationToken cancellationToken = default);

    Task<UserProactivePreferenceInputViewModel> GetInputAsync(string userId, string userDisplayName, CancellationToken cancellationToken = default);

    Task SaveAsync(UserProactivePreferenceInputViewModel model, CancellationToken cancellationToken = default);
}

public class UserProactivePreferenceService(
    AppDbContext context,
    IAiSettingsService aiSettingsService) : IUserProactivePreferenceService
{
    public async Task<UserProactivePreference> GetAsync(string userId, string userDisplayName, CancellationToken cancellationToken = default)
    {
        var preference = await context.UserProactivePreferences.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preference != null)
        {
            if (!string.Equals(preference.UserDisplayName, userDisplayName, StringComparison.Ordinal))
            {
                preference.UserDisplayName = userDisplayName;
                preference.UpdatedUtc = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
            }

            return preference;
        }

        var ai = await aiSettingsService.GetAsync(cancellationToken);
        preference = new UserProactivePreference
        {
            UserId = userId,
            UserDisplayName = userDisplayName,
            IsOptedIn = !ai.RequireUserOptInForProactiveAssist,
            NextHourEnabled = ai.EnableNextHourRecommendations,
            TomorrowDigestEnabled = ai.EnableTomorrowRecommendations,
            PreEventReminderEnabled = ai.EnablePreEventReminders,
            ReminderMinutesBefore = ai.PreEventReminderMinutes,
            PreferredMorningDigestTime = ai.MorningDigestTime,
            PreferredAfternoonDigestTime = ai.AfternoonDigestTime,
            QuietHoursStart = ai.QuietHoursStart,
            QuietHoursEnd = ai.QuietHoursEnd,
            PreferredChannels = ai.SendInAppNotifications ? "InApp" : string.Empty,
            TimeZone = ai.DefaultUserTimeZone
        };

        context.UserProactivePreferences.Add(preference);
        await context.SaveChangesAsync(cancellationToken);
        return preference;
    }

    public async Task<UserProactivePreferenceInputViewModel> GetInputAsync(string userId, string userDisplayName, CancellationToken cancellationToken = default)
    {
        var preference = await GetAsync(userId, userDisplayName, cancellationToken);
        return new UserProactivePreferenceInputViewModel
        {
            UserId = preference.UserId,
            UserDisplayName = preference.UserDisplayName,
            IsOptedIn = preference.IsOptedIn,
            NextHourEnabled = preference.NextHourEnabled,
            TomorrowDigestEnabled = preference.TomorrowDigestEnabled,
            PreEventReminderEnabled = preference.PreEventReminderEnabled,
            ReminderMinutesBefore = preference.ReminderMinutesBefore,
            PreferredMorningDigestTime = preference.PreferredMorningDigestTime,
            PreferredAfternoonDigestTime = preference.PreferredAfternoonDigestTime,
            QuietHoursStart = preference.QuietHoursStart,
            QuietHoursEnd = preference.QuietHoursEnd,
            PreferredChannels = preference.PreferredChannels,
            TimeZone = preference.TimeZone
        };
    }

    public async Task SaveAsync(UserProactivePreferenceInputViewModel model, CancellationToken cancellationToken = default)
    {
        var preference = await GetAsync(model.UserId, model.UserDisplayName, cancellationToken);
        preference.UserDisplayName = model.UserDisplayName;
        preference.IsOptedIn = model.IsOptedIn;
        preference.NextHourEnabled = model.NextHourEnabled;
        preference.TomorrowDigestEnabled = model.TomorrowDigestEnabled;
        preference.PreEventReminderEnabled = model.PreEventReminderEnabled;
        preference.ReminderMinutesBefore = model.ReminderMinutesBefore;
        preference.PreferredMorningDigestTime = model.PreferredMorningDigestTime;
        preference.PreferredAfternoonDigestTime = model.PreferredAfternoonDigestTime;
        preference.QuietHoursStart = model.QuietHoursStart;
        preference.QuietHoursEnd = model.QuietHoursEnd;
        preference.PreferredChannels = model.PreferredChannels;
        preference.TimeZone = model.TimeZone;
        preference.UpdatedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }
}
