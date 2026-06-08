using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class CachedEmailMessage
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string MessageId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Subject { get; set; }

    [MaxLength(500)]
    public string? From { get; set; }

    public DateTimeOffset? ReceivedAt { get; set; }

    [MaxLength(4000)]
    public string? Snippet { get; set; }

    [MaxLength(500)]
    public string? WebUrl { get; set; }

    public string? HtmlBody { get; set; }

    public string? TextBody { get; set; }

    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}
