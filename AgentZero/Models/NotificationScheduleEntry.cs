using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public enum NotificationType
{
    NextHour,
    TomorrowDigest,
    PreEventReminder,
    RiskAlert,
    FollowUpReminder
}

public enum NotificationStatus
{
    Pending,
    Processing,
    Sent,
    Cancelled,
    Failed,
    Snoozed
}

public class NotificationScheduleEntry
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string UserDisplayName { get; set; } = string.Empty;

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public int? TaskId { get; set; }
    public ScheduleEvent? Task { get; set; }

    public NotificationType NotificationType { get; set; }

    public DateTime ScheduledForUtc { get; set; }

    [MaxLength(80)]
    public string TimeZone { get; set; } = "UTC";

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public int Priority { get; set; } = 1;

    [MaxLength(8000)]
    public string? ContextSnapshotJson { get; set; }

    public DateTime LastComputedUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(80)]
    public string TriggerSource { get; set; } = "Manual";

    [MaxLength(250)]
    public string DeduplicationKey { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ActionUrl { get; set; }

    public int RetryCount { get; set; }

    public bool IsDismissed { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
