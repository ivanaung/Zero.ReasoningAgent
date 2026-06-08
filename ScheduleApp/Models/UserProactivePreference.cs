using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class UserProactivePreference
{
    [Key]
    [MaxLength(120)]
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

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
