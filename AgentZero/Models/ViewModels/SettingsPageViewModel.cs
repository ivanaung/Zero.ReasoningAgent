using ScheduleApp.Models;

namespace ScheduleApp.Models.ViewModels;

public class SettingsPageViewModel
{
    public UserSettings Preferences { get; set; } = new();

    public AiSettingsInputViewModel Ai { get; set; } = new();

    public GoogleIntegrationSettingsInputViewModel Integration { get; set; } = new();

    public McpAccessSettingsViewModel Mcp { get; set; } = new();

    public GenerateMcpApiKeyInputViewModel McpGenerate { get; set; } = new();

    public ZeroAssistantSettingsInputViewModel Zero { get; set; } = new();

    public OperationalDatabaseInputViewModel Database { get; set; } = new();

    public OperationalDatabaseTestResultViewModel? DatabaseTestResult { get; set; }

    public MarketDataSettingsInputViewModel MarketData { get; set; } = new();

    public MarketDataProviderTestResultViewModel? MarketDataTestResult { get; set; }

    public bool Saved { get; set; }

    public bool AiSaved { get; set; }

    public bool IntegrationSaved { get; set; }

    public bool McpSaved { get; set; }

    public bool ZeroSaved { get; set; }

    public bool DatabaseSaved { get; set; }

    public bool MarketDataSaved { get; set; }

    public string ActiveTab { get; set; } = "display";

    public string? AiHealthStatus { get; set; }

    public string? AiHealthMessage { get; set; }

    public IReadOnlyList<AiToolDebugItemViewModel> AvailableTools { get; set; } = [];
}

public class AiToolDebugItemViewModel
{
    public string Name { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
