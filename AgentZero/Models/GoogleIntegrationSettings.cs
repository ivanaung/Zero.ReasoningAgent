using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScheduleApp.Models;

public class GoogleIntegrationSettings
{
    public int Id { get; set; } = 1;

    public bool IsEnabled { get; set; }

    [MaxLength(200)]
    public string? ClientId { get; set; }

    [MaxLength(500)]
    public string? PublicBaseUrl { get; set; }

    [MaxLength(4000)]
    public string? ClientSecretEncrypted { get; set; }

    [MaxLength(4000)]
    public string? RefreshTokenEncrypted { get; set; }

    [MaxLength(4000)]
    public string? AccessTokenEncrypted { get; set; }

    [MaxLength(320)]
    public string? ConnectedEmail { get; set; }

    [MaxLength(200)]
    public string ScopeSet { get; set; } = "openid email profile https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/calendar.readonly";

    public bool EnableEmailIntegration { get; set; } = true;

    public bool EnableCalendarIntegration { get; set; } = true;

    public bool EnableAiEmailTools { get; set; } = true;

    public bool EnableAiCalendarTools { get; set; } = true;

    public int InboxCacheLimit { get; set; } = 100;

    public DateTime? AccessTokenExpiresUtc { get; set; }

    public DateTime? ConnectedAtUtc { get; set; }

    public DateTime? LastSyncUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string? ClientSecret { get; set; }

    [NotMapped]
    public string? RefreshToken { get; set; }

    [NotMapped]
    public string? AccessToken { get; set; }
}
