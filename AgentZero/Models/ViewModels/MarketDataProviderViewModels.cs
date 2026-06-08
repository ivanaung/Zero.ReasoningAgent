using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models.ViewModels;

public class MarketDataSettingsInputViewModel
{
    public List<MarketDataProviderInputItemViewModel> Providers { get; set; } = new();
}

public class MarketDataProviderInputItemViewModel
{
    public int Id { get; set; }

    public MarketDataProviderName ProviderName { get; set; }

    public string ProviderLabel { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public bool IsPrimary { get; set; }

    [MaxLength(500)]
    public string? ApiKey { get; set; }

    public bool ClearApiKey { get; set; }

    public bool HasApiKey { get; set; }

    public string MaskedApiKey { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public string Status { get; set; } = "Not configured";

    public string? LastTestResult { get; set; }

    public DateTime? LastTestDate { get; set; }

    public bool SupportsApiKey { get; set; }

    public bool IsExperimental { get; set; }
}

public class MarketDataProviderTestResultViewModel
{
    public MarketDataProviderName ProviderName { get; set; }

    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal? QuotePrice { get; set; }

    public DateTime TestedAtUtc { get; set; } = DateTime.UtcNow;
}

public class MarketDataProviderStatusViewModel
{
    public bool HasConfiguredProvider { get; set; }

    public string ProviderLabel { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string WarningMessage { get; set; } = string.Empty;

    public string LastTestSummary { get; set; } = string.Empty;

    public DateTime? LastTestDateUtc { get; set; }
}
