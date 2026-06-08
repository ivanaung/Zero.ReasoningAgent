using System.ComponentModel.DataAnnotations;
using ScheduleApp.Models;

namespace ScheduleApp.Models.ViewModels;

public class AiSettingsInputViewModel
{
    public bool IsEnabled { get; set; }

    [Required]
    public AiProviderType ProviderType { get; set; } = AiProviderType.Ollama;

    [Required]
    [MaxLength(200)]
    public string ModelId { get; set; } = "llama3.2";

    [Required]
    [MaxLength(500)]
    public string EndpointUrl { get; set; } = "http://localhost:11434";

    [MaxLength(2000)]
    public string? ApiKey { get; set; }

    public bool ClearApiKey { get; set; }

    [Range(0, 2)]
    public double Temperature { get; set; } = 0.2d;

    [Range(1, 16000)]
    public int MaxTokens { get; set; } = 1200;

    [Required]
    [MaxLength(8000)]
    public string SystemPrompt { get; set; } = string.Empty;

    public bool EnableToolCalling { get; set; } = true;

    public bool EnableStreaming { get; set; } = true;

    public bool EnableProgressMonitoring { get; set; } = true;

    [Required]
    [MaxLength(80)]
    public string DefaultTimeZone { get; set; } = "Pacific/Auckland";

    [Required]
    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$", ErrorMessage = "Use HH:mm format.")]
    public string WorkingHoursStart { get; set; } = "08:00";

    [Required]
    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$", ErrorMessage = "Use HH:mm format.")]
    public string WorkingHoursEnd { get; set; } = "17:00";

    [Required]
    [MaxLength(64)]
    public string WorkingDays { get; set; } = "Mon,Tue,Wed,Thu,Fri";

    public bool AutoCreateLowRiskTasks { get; set; }

    public bool RequireApprovalForScheduleChange { get; set; } = true;

    public bool RequireApprovalForTaskDeletion { get; set; } = true;

    public bool EnableProactiveAssist { get; set; }

    public bool EnableNextHourRecommendations { get; set; } = true;

    public bool EnableTomorrowRecommendations { get; set; } = true;

    public bool EnablePreEventReminders { get; set; } = true;

    [Range(1, 240)]
    public int PreEventReminderMinutes { get; set; } = 15;

    [Required]
    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$", ErrorMessage = "Use HH:mm format.")]
    public string MorningDigestTime { get; set; } = "08:30";

    [Required]
    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$", ErrorMessage = "Use HH:mm format.")]
    public string AfternoonDigestTime { get; set; } = "16:30";

    [Range(1, 10)]
    public int MaxRecommendationsPerNotification { get; set; } = 3;

    [Required]
    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$", ErrorMessage = "Use HH:mm format.")]
    public string QuietHoursStart { get; set; } = "22:00";

    [Required]
    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$", ErrorMessage = "Use HH:mm format.")]
    public string QuietHoursEnd { get; set; } = "06:00";

    public bool SendInAppNotifications { get; set; } = true;

    public bool SendPushNotifications { get; set; }

    public bool SendEmailNotifications { get; set; }

    public bool EnableAiEnrichmentForNotifications { get; set; } = true;

    [Range(1, 168)]
    public int NotificationLookaheadHours { get; set; } = 24;

    [Range(1, 14)]
    public int DigestLookaheadDays { get; set; } = 1;

    public bool RecomputeOnTaskUpdate { get; set; } = true;

    public bool RecomputeOnAssignmentChange { get; set; } = true;

    public bool RecomputeOnDependencyChange { get; set; } = true;

    public bool RequireUserOptInForProactiveAssist { get; set; } = true;

    [Required]
    [MaxLength(80)]
    public string DefaultUserTimeZone { get; set; } = "Pacific/Auckland";

    public string ApiKeyPlaceholder => string.IsNullOrWhiteSpace(ApiKey) ? string.Empty : "••••••••";
}
