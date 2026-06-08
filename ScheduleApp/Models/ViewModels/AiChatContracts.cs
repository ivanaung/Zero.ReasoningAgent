namespace ScheduleApp.Models.ViewModels;

public class AiChatRequest
{
    public string Message { get; set; } = string.Empty;

    public string? ConversationId { get; set; }

    public bool Stream { get; set; }
}

public class AiChatResponse
{
    public string ConversationId { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool RequiresApproval { get; set; }

    public string ProviderLabel { get; set; } = string.Empty;

    public string? ModelId { get; set; }

    public IReadOnlyList<AiActionItemViewModel> Actions { get; init; } = [];
}

public class AiActionItemViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string Status { get; set; } = "info";
}

public class AiHealthViewModel
{
    public bool Enabled { get; set; }

    public bool Healthy { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public class ProjectMonitoringResultViewModel
{
    public int ProjectCount { get; set; }

    public int OverdueTaskCount { get; set; }

    public int BlockedTaskCount { get; set; }

    public int MilestonesAtRisk { get; set; }

    public int AutoCreatedTasks { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string? AgentSummary { get; set; }
}
