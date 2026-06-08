using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Modules.Trading;

public enum TradingJournalActionType
{
    Buy = 0,
    Sell = 1,
    Hold = 2,
    Watch = 3,
    Research = 4
}

public enum TradingAdvisorSuggestion
{
    Buy = 0,
    Hold = 1,
    Trim = 2,
    Avoid = 3
}

public class TradingHolding
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Market { get; set; } = string.Empty;

    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal AverageCost { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public decimal CurrentPrice { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class TradingWatchlistItem
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Market { get; set; } = string.Empty;

    public decimal? TargetBuyPrice { get; set; }

    public decimal? TargetSellPrice { get; set; }

    public decimal? AlertBelowPrice { get; set; }

    public decimal? AlertAbovePrice { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class TradingJournalEntry
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;

    public TradingJournalActionType ActionType { get; set; }

    public decimal? Price { get; set; }

    public decimal? Quantity { get; set; }

    [MaxLength(2000)]
    public string? Reason { get; set; }

    [MaxLength(200)]
    public string? Emotion { get; set; }

    [MaxLength(2000)]
    public string? LessonLearned { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class TradingAdvisorSnapshot
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ProviderLabel { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ModelId { get; set; }

    [MaxLength(80)]
    public string RiskLevel { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ConcentrationRisk { get; set; } = string.Empty;

    [MaxLength(80)]
    public string PossibleNextAction { get; set; } = string.Empty;

    public TradingAdvisorSuggestion Suggestion { get; set; }

    [MaxLength(4000)]
    public string PortfolioSummary { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Reasoning { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Disclaimer { get; set; } = string.Empty;

    [MaxLength(8000)]
    public string RawJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class TradingPriceSnapshot
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Market { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public decimal Price { get; set; }

    [MaxLength(120)]
    public string Source { get; set; } = "Saved";

    public bool IsLive { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.Now;
}
