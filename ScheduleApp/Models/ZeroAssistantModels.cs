using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class ZeroAssistantSettings
{
    public int Id { get; set; } = 1;

    public bool EnableVoice { get; set; } = true;

    public bool EnableLocalFileTools { get; set; }

    public bool EnableVisionTools { get; set; }

    public ZeroSearchProvider SearchProvider { get; set; } = ZeroSearchProvider.SearXNG;

    [MaxLength(500)]
    public string WhisperUrl { get; set; } = "http://localhost:10300";

    [MaxLength(500)]
    public string PiperUrl { get; set; } = "http://localhost:10200";

    [MaxLength(120)]
    public string PiperEndpoint { get; set; } = "/";

    [MaxLength(200)]
    public string PiperVoice { get; set; } = string.Empty;

    [MaxLength(500)]
    public string SearXngBaseUrl { get; set; } = "http://localhost:10100";

    [Range(1, 100)]
    public int HistoryLimit { get; set; } = 12;

    [Range(5, 300)]
    public int RequestTimeoutSeconds { get; set; } = 120;

    [Range(0.5, 2.0)]
    public double BrowserSpeechRate { get; set; } = 0.94d;

    [Range(0.5, 2.0)]
    public double BrowserSpeechPitch { get; set; } = 0.98d;

    [Range(1, 30)]
    public int MemoryLimit { get; set; } = 30;

    [Range(1, 25)]
    public int MaxUploadMegabytes { get; set; } = 25;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LegacyImportCompletedUtc { get; set; }
}

public enum ZeroSearchProvider
{
    SearXNG = 1
}

public class ZeroMemoryItem
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Text { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public class ZeroConversationMessage
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ConversationId { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Role { get; set; } = string.Empty;

    [MaxLength(8000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
