using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class NotificationDeliveryLog
{
    public int Id { get; set; }

    public int NotificationScheduleEntryId { get; set; }
    public NotificationScheduleEntry? NotificationScheduleEntry { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Channel { get; set; } = "InApp";

    public DateTime SentUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(40)]
    public string Status { get; set; } = "Sent";

    [MaxLength(120)]
    public string? ProviderModel { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }
}
