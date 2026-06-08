using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models.ViewModels;

public class ZeroAssistantSettingsInputViewModel
{
    public bool EnableVoice { get; set; } = true;

    public bool EnableLocalFileTools { get; set; }

    public bool EnableVisionTools { get; set; }

    public ZeroSearchProvider SearchProvider { get; set; } = ZeroSearchProvider.SearXNG;

    [Required]
    [MaxLength(500)]
    public string WhisperUrl { get; set; } = "http://localhost:10300";

    [Required]
    [MaxLength(500)]
    public string PiperUrl { get; set; } = "http://localhost:10200";

    [Required]
    [MaxLength(120)]
    public string PiperEndpoint { get; set; } = "/";

    [MaxLength(200)]
    public string PiperVoice { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string SearXngBaseUrl { get; set; } = "http://localhost:10100";

    public IReadOnlyList<ZeroAssistantPiperVoiceViewModel> PiperVoices { get; set; } = [];

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
}

public class ZeroAssistantPageViewModel
{
    public IReadOnlyList<ZeroAssistantActionViewModel> Actions { get; set; } = [];

    public IReadOnlyList<string> Memory { get; set; } = [];

    public ZeroAssistantSettingsInputViewModel Settings { get; set; } = new();
}

public sealed record ZeroConversationHistoryViewModel(
    string ConversationId,
    IReadOnlyList<ZeroConversationTurnViewModel> Messages);

public sealed record ZeroConversationTurnViewModel(
    string Role,
    string Content,
    DateTime CreatedUtc);

public sealed record ZeroAssistantPiperVoiceViewModel(string Name, string Label);

public class ZeroAssistantActionViewModel
{
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string PromptHint { get; set; } = string.Empty;
}

public class ZeroAssistantReplyViewModel
{
    public string ConversationId { get; set; } = string.Empty;

    public string UserText { get; set; } = string.Empty;

    public string ReplyText { get; set; } = string.Empty;

    public string Status { get; set; } = "idle";

    public string ProviderLabel { get; set; } = string.Empty;

    public string? ModelId { get; set; }

    public string? AudioDataUrl { get; set; }

    public ZeroAssistantPanelTabViewModel? PanelTab { get; set; }
}

public class ZeroAssistantPanelTabViewModel
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public FileSearchResultViewModel? FileSearch { get; set; }

    public StorageUsageResultViewModel? StorageUsage { get; set; }

    public WebSearchPanelViewModel? WebSearch { get; set; }
}

public sealed record FileSearchResultViewModel(
    string TabTitle,
    string Summary,
    string Query,
    IReadOnlyList<string> Roots,
    IReadOnlyList<FileSearchMatchViewModel> Matches,
    bool Truncated,
    bool TimedOut);

public sealed record FileSearchMatchViewModel(
    string Name,
    string Path,
    string Kind,
    string ParentPath,
    long? SizeBytes,
    DateTimeOffset? LastModifiedUtc);

public sealed record StorageUsageResultViewModel(
    string TabTitle,
    string Summary,
    IReadOnlyList<string> Roots,
    IReadOnlyList<StorageUsageItemViewModel> TopFiles,
    IReadOnlyList<StorageUsageItemViewModel> TopFolders,
    int FilesScanned,
    long TotalBytesScanned,
    bool TimedOut);

public sealed record StorageUsageItemViewModel(
    string Name,
    string Path,
    string Kind,
    long SizeBytes,
    DateTimeOffset? LastModifiedUtc);

public sealed record WebSearchPanelViewModel(
    string Query,
    IReadOnlyList<WebSearchPanelResultViewModel> Results,
    bool Success,
    string? ErrorMessage,
    long ElapsedMilliseconds);

public sealed record WebSearchPanelResultViewModel(
    string Title,
    string Url,
    string Snippet,
    string Source);
