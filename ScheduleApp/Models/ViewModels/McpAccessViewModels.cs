using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models.ViewModels;

public class McpAccessSettingsViewModel
{
    public string EndpointPath { get; set; } = "/mcp";

    public string HeaderName { get; set; } = "X-Api-Key";

    public string? NewlyGeneratedApiKey { get; set; }

    public IReadOnlyList<McpApiKeyListItemViewModel> Keys { get; set; } = [];
}

public class McpApiKeyListItemViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }
}

public class GenerateMcpApiKeyInputViewModel
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = "External Agent";
}
