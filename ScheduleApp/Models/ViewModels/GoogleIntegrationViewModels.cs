using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models.ViewModels;

public class GoogleIntegrationSettingsInputViewModel
{
    public bool IsEnabled { get; set; }

    [MaxLength(200)]
    public string? ClientId { get; set; }

    [MaxLength(500)]
    public string? PublicBaseUrl { get; set; }

    [MaxLength(2000)]
    public string? ClientSecret { get; set; }

    public bool ClearClientSecret { get; set; }

    [MaxLength(200)]
    public string ScopeSet { get; set; } = "openid email profile https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/calendar.readonly";

    public bool EnableEmailIntegration { get; set; } = true;

    public bool EnableCalendarIntegration { get; set; } = true;

    public bool EnableAiEmailTools { get; set; } = true;

    public bool EnableAiCalendarTools { get; set; } = true;

    [Range(10, 500)]
    public int InboxCacheLimit { get; set; } = 100;

    public string? ConnectedEmail { get; set; }

    public bool IsConnected { get; set; }

    public DateTime? ConnectedAtUtc { get; set; }

    public DateTime? LastSyncUtc { get; set; }

    public string RedirectUri { get; set; } = string.Empty;
}

public class GoogleConnectionStatusViewModel
{
    public bool IsEnabled { get; set; }

    public bool IsConfigured { get; set; }

    public bool HasPublicBaseUrl { get; set; }

    public bool IsConnected { get; set; }

    public bool EmailEnabled { get; set; }

    public bool CalendarEnabled { get; set; }

    public string? ConnectedEmail { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public DateTime? ConnectedAtUtc { get; set; }

    public DateTime? LastSyncUtc { get; set; }

    public int InboxCacheLimit { get; set; } = 100;
}

public class GoogleEmailMessageViewModel
{
    public string Id { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public DateTimeOffset? ReceivedAt { get; set; }

    public string Snippet { get; set; } = string.Empty;

    public string WebUrl { get; set; } = string.Empty;

    public string HtmlBody { get; set; } = string.Empty;

    public string TextBody { get; set; } = string.Empty;
}

public class GoogleCalendarEventViewModel
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Organizer { get; set; }

    public DateTimeOffset? Start { get; set; }

    public DateTimeOffset? End { get; set; }

    public string? HtmlLink { get; set; }
}

public class EmailHubViewModel
{
    public GoogleConnectionStatusViewModel Status { get; set; } = new();

    public IReadOnlyList<GoogleEmailMessageViewModel> InboxMessages { get; set; } = [];

    public int InboxDisplayLimit { get; set; } = 100;

    public IReadOnlyList<GoogleCalendarEventViewModel> UpcomingEvents { get; set; } = [];

    public string? InboxError { get; set; }

    public string? CalendarError { get; set; }
}
