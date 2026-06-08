namespace ScheduleApp.Modules.Trading;

public class TradingHoldingViewModel
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal TotalCost { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedProfitLoss { get; set; }
    public decimal UnrealizedProfitLossPercent { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TradingWatchlistItemViewModel
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public decimal? CurrentPrice { get; set; }
    public decimal? TargetBuyPrice { get; set; }
    public decimal? TargetSellPrice { get; set; }
    public decimal? AlertBelowPrice { get; set; }
    public decimal? AlertAbovePrice { get; set; }
    public bool IsActive { get; set; }
    public string AlertStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TradingAdvisorResultViewModel
{
    public string ProviderLabel { get; set; } = string.Empty;
    public string? ModelId { get; set; }
    public string PortfolioSummary { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string ConcentrationRisk { get; set; } = string.Empty;
    public string PossibleNextAction { get; set; } = string.Empty;
    public TradingAdvisorSuggestion Suggestion { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string Disclaimer { get; set; } = string.Empty;
    public bool UsedAi { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}

public class TradingDashboardViewModel
{
    public List<TradingHoldingViewModel> Holdings { get; set; } = new();
    public List<TradingWatchlistItemViewModel> Watchlist { get; set; } = new();
    public List<TradingJournalEntry> RecentJournalEntries { get; set; } = new();
    public List<TradingAdvisorSnapshot> AdvisorHistory { get; set; } = new();
    public TradingAdvisorResultViewModel? LatestAdvice { get; set; }
    public string PriceWarning { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    public decimal TotalMarketValue { get; set; }
    public decimal TotalUnrealizedProfitLoss { get; set; }
    public decimal TotalUnrealizedProfitLossPercent => TotalCost == 0m ? 0m : TotalUnrealizedProfitLoss / TotalCost * 100m;
}

public class TradingPortfolioPageViewModel
{
    public List<TradingHoldingViewModel> Holdings { get; set; } = new();
    public string PriceWarning { get; set; } = string.Empty;
}

public class TradingWatchlistPageViewModel
{
    public List<TradingWatchlistItemViewModel> Items { get; set; } = new();
    public string PriceWarning { get; set; } = string.Empty;
}

public class TradingJournalPageViewModel
{
    public List<TradingJournalEntry> Entries { get; set; } = new();
}

public class TradingAdvisorPageViewModel
{
    public TradingAdvisorResultViewModel? LatestAdvice { get; set; }
    public List<TradingAdvisorSnapshot> History { get; set; } = new();
    public string PriceWarning { get; set; } = string.Empty;
}

public class TradingHoldingInputModel
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal? CurrentPrice { get; set; }
    public string? Notes { get; set; }
}

public class TradingWatchlistInputModel
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public decimal? TargetBuyPrice { get; set; }
    public decimal? TargetSellPrice { get; set; }
    public decimal? AlertBelowPrice { get; set; }
    public decimal? AlertAbovePrice { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TradingJournalEntryInputModel
{
    public string Symbol { get; set; } = string.Empty;
    public TradingJournalActionType ActionType { get; set; }
    public decimal? Price { get; set; }
    public decimal? Quantity { get; set; }
    public string? Reason { get; set; }
    public string? Emotion { get; set; }
    public string? LessonLearned { get; set; }
}

public class TradingPriceResult
{
    public string Symbol { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool IsLive { get; set; }
    public string? ProviderSymbol { get; set; }
    public string? FailureReason { get; set; }
}
