using System.ComponentModel.DataAnnotations;
using ScheduleApp.Models;

namespace ScheduleApp.Models.ViewModels;

public enum RecommendationHorizon
{
    NextHour,
    Tomorrow,
    NeedsAttention,
    PreEventReminder,
    RiskFollowUp
}

public class RecommendationRequest
{
    public string UserId { get; set; } = string.Empty;

    public string UserDisplayName { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "Pacific/Auckland";

    public RecommendationHorizon Horizon { get; set; }

    public DateTimeOffset ReferenceTime { get; set; } = DateTimeOffset.UtcNow;
}

public class RecommendationContextViewModel
{
    public string UserId { get; set; } = string.Empty;

    public string UserDisplayName { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "Pacific/Auckland";

    public RecommendationHorizon Horizon { get; set; }

    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<RecommendationItemViewModel> DueSoonTasks { get; set; } = [];

    public List<RecommendationItemViewModel> OverdueTasks { get; set; } = [];

    public List<RecommendationItemViewModel> BlockedTasks { get; set; } = [];

    public List<RecommendationItemViewModel> UpcomingMilestones { get; set; } = [];

    public List<RecommendationItemViewModel> UpcomingAssignments { get; set; } = [];

    public List<RecommendationItemViewModel> RiskItems { get; set; } = [];

    public string CalendarSummary { get; set; } = string.Empty;
}

public class RecommendationResultViewModel
{
    public RecommendationHorizon Horizon { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public bool UsedAiEnrichment { get; set; }

    public List<RecommendationItemViewModel> Items { get; set; } = [];

    public string? SourceNotificationType { get; set; }
}

public class RecommendationItemViewModel
{
    public int? TaskId { get; set; }

    public int? ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Priority { get; set; } = string.Empty;

    public DateTimeOffset? StartUtc { get; set; }

    public DateTimeOffset? EndUtc { get; set; }

    public string? ActionUrl { get; set; }
}

public class UserProactivePreferenceInputViewModel
{
    public string UserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string UserDisplayName { get; set; } = string.Empty;

    public bool IsOptedIn { get; set; }

    public bool NextHourEnabled { get; set; } = true;

    public bool TomorrowDigestEnabled { get; set; } = true;

    public bool PreEventReminderEnabled { get; set; } = true;

    [Range(1, 240)]
    public int ReminderMinutesBefore { get; set; } = 15;

    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$")]
    public string PreferredMorningDigestTime { get; set; } = "08:30";

    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$")]
    public string PreferredAfternoonDigestTime { get; set; } = "16:30";

    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$")]
    public string QuietHoursStart { get; set; } = "22:00";

    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$")]
    public string QuietHoursEnd { get; set; } = "06:00";

    [MaxLength(120)]
    public string PreferredChannels { get; set; } = "InApp";

    [MaxLength(80)]
    public string TimeZone { get; set; } = "Pacific/Auckland";
}

public class NotificationCenterItemViewModel
{
    public int Id { get; set; }

    public NotificationType NotificationType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset ScheduledForUtc { get; set; }

    public NotificationStatus Status { get; set; }

    public string? ActionUrl { get; set; }

    public int? TaskId { get; set; }

    public int? ProjectId { get; set; }
}
