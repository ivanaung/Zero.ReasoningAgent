using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class AiActionAudit
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = "anonymous";

    [MaxLength(120)]
    public string UserDisplayName { get; set; } = "Anonymous";

    [MaxLength(120)]
    public string ActionType { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Provider { get; set; }

    [MaxLength(200)]
    public string? ModelId { get; set; }

    [MaxLength(4000)]
    public string Summary { get; set; } = string.Empty;

    [MaxLength(8000)]
    public string? RequestPreview { get; set; }

    [MaxLength(8000)]
    public string? ResponsePreview { get; set; }

    [MaxLength(40)]
    public string Outcome { get; set; } = "Succeeded";

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
