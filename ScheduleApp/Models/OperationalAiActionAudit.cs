namespace ScheduleApp.Models;

public class OperationalAiActionAudit
{
    public int Id { get; set; }
    public string UserId { get; set; } = "anonymous";
    public string UserDisplayName { get; set; } = "Anonymous";
    public string ActionType { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ModelId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? RequestPreview { get; set; }
    public string? ResponsePreview { get; set; }
    public string Outcome { get; set; } = "Succeeded";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
