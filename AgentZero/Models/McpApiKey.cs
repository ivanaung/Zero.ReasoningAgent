using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class McpApiKey
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(32)]
    public string KeyPrefix { get; set; } = string.Empty;

    [MaxLength(200)]
    public string KeyHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
