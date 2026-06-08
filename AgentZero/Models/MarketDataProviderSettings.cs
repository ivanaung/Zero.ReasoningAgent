using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScheduleApp.Models;

public enum MarketDataProviderName
{
    MockData = 0,
    AlphaVantage = 1,
    Finnhub = 2,
    YahooFinance = 3,
    CoinGecko = 4
}

public class MarketDataProviderSettings
{
    public int Id { get; set; }

    public MarketDataProviderName ProviderName { get; set; }

    public bool IsEnabled { get; set; }

    public bool IsPrimary { get; set; }

    [MaxLength(4000)]
    public string? ApiKeyEncrypted { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? LastTestResult { get; set; }

    public DateTime? LastTestDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string? ApiKey { get; set; }
}
