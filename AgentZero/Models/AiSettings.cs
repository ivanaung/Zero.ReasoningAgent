using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScheduleApp.Models;

public enum AiProviderType
{
    Ollama = 0,
    OpenAI = 1,
    AzureOpenAI = 2,
    Anthropic = 3
}

public class AiSettings
{
    public int Id { get; set; } = 1;

    public bool IsEnabled { get; set; }

    public AiProviderType ProviderType { get; set; } = AiProviderType.Ollama;

    [MaxLength(200)]
    public string ModelId { get; set; } = "llama3.2";

    [MaxLength(500)]
    public string EndpointUrl { get; set; } = "http://localhost:11434";

    [MaxLength(4000)]
    public string? ApiKeyEncrypted { get; set; }

    [Range(0, 2)]
    public double Temperature { get; set; } = 0.2d;

    [Range(1, 16000)]
    public int MaxTokens { get; set; } = 1200;

    [MaxLength(8000)]
    public string SystemPrompt { get; set; } = """
You are the Project Management Assistant.
You help users manage projects and tasks using approved tools.
Never invent project data, dates, or status.
Use tools for all live information and all actions.
For task-specific questions that mention a name/title and ask about due dates, expiry, end dates, status, blocked state, or completion, search tasks first using the likely task title keywords instead of the full user sentence.
Example: if the user asks "when our Rejo expired", search for "Rejo", inspect the matching task, and answer from its actual end date and status.
When Google email/calendar integration is connected, use the dedicated inbox and linked-calendar tools for live email and calendar questions instead of guessing from project data.
Respect permissions and business rules.
When creating or rescheduling tasks, rely on system scheduling rules.
Be concise, clear, and operational.
If an action is risky or requires approval, explain why and propose the next step.
""";

    public bool EnableToolCalling { get; set; } = true;

    public bool EnableStreaming { get; set; } = true;

    public bool EnableProgressMonitoring { get; set; } = true;

    [MaxLength(80)]
    public string DefaultTimeZone { get; set; } = "Pacific/Auckland";

    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$")]
    public string WorkingHoursStart { get; set; } = "08:00";

    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$")]
    public string WorkingHoursEnd { get; set; } = "17:00";

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

    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$")]
    public string MorningDigestTime { get; set; } = "08:30";

    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$")]
    public string AfternoonDigestTime { get; set; } = "16:30";

    [Range(1, 10)]
    public int MaxRecommendationsPerNotification { get; set; } = 3;

    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$")]
    public string QuietHoursStart { get; set; } = "22:00";

    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$")]
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

    [MaxLength(80)]
    public string DefaultUserTimeZone { get; set; } = "Pacific/Auckland";

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string? ApiKey { get; set; }
}
