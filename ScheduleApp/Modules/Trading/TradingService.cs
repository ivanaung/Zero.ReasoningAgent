using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;
using ScheduleApp.Services;

namespace ScheduleApp.Modules.Trading;

public interface ITradingService
{
    Task<TradingDashboardViewModel> GetDashboardAsync(string userId, CancellationToken cancellationToken = default);
    Task<TradingPortfolioPageViewModel> GetPortfolioAsync(string userId, CancellationToken cancellationToken = default);
    Task<TradingWatchlistPageViewModel> GetWatchlistAsync(string userId, CancellationToken cancellationToken = default);
    Task<TradingJournalPageViewModel> GetJournalAsync(string userId, CancellationToken cancellationToken = default);
    Task<TradingAdvisorPageViewModel> GetAdvisorAsync(string userId, CancellationToken cancellationToken = default);
    Task<List<TradingHoldingViewModel>> GetHoldingsAsync(string userId, CancellationToken cancellationToken = default);
    Task<TradingHolding> SaveHoldingAsync(string userId, TradingHoldingInputModel model, CancellationToken cancellationToken = default);
    Task DeleteHoldingAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task<List<TradingWatchlistItemViewModel>> GetWatchlistItemsAsync(string userId, CancellationToken cancellationToken = default);
    Task<TradingWatchlistItem> SaveWatchlistItemAsync(string userId, TradingWatchlistInputModel model, CancellationToken cancellationToken = default);
    Task DeleteWatchlistItemAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task<List<TradingJournalEntry>> GetJournalEntriesAsync(string userId, CancellationToken cancellationToken = default);
    Task<TradingJournalEntry> AddJournalEntryAsync(string userId, TradingJournalEntryInputModel model, CancellationToken cancellationToken = default);
    Task<TradingAdvisorResultViewModel> GenerateAdviceAsync(string userId, bool saveToHistory = true, CancellationToken cancellationToken = default);
    Task<List<TradingPriceResult>> RefreshPricesAsync(string userId, CancellationToken cancellationToken = default);
    Task<string> GetPriceWarningAsync(string userId, CancellationToken cancellationToken = default);
}

public interface IMarketPriceService
{
    Task<(List<TradingPriceResult> Prices, string Warning)> RefreshAsync(
        string userId,
        IReadOnlyCollection<(string Symbol, string Market, string Currency, decimal SavedPrice)> requests,
        CancellationToken cancellationToken = default);

    Task<MarketDataProviderStatusViewModel> GetStatusAsync(CancellationToken cancellationToken = default);
}

public interface ITradingAdvisorService
{
    Task<TradingAdvisorResultViewModel> GenerateAsync(TradingDashboardViewModel dashboard, CancellationToken cancellationToken = default);
}

public class TradingService(
    AppDbContext context,
    IMarketPriceService marketPriceService,
    ITradingAdvisorService tradingAdvisorService,
    ILogger<TradingService> logger) : ITradingService
{
    public async Task<TradingDashboardViewModel> GetDashboardAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(userId, cancellationToken);
        var holdings = await GetHoldingsAsync(userId, cancellationToken);
        var watchlist = await GetWatchlistItemsAsync(userId, cancellationToken);
        var journal = await GetJournalEntriesAsync(userId, cancellationToken);
        var history = await context.Set<TradingAdvisorSnapshot>()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new TradingDashboardViewModel
        {
            Holdings = holdings,
            Watchlist = watchlist,
            RecentJournalEntries = journal.Take(10).ToList(),
            AdvisorHistory = history,
            LatestAdvice = history.Count == 0 ? null : MapAdvice(history[0]),
            PriceWarning = await GetPriceWarningAsync(userId, cancellationToken),
            TotalCost = holdings.Sum(item => item.TotalCost),
            TotalMarketValue = holdings.Sum(item => item.MarketValue),
            TotalUnrealizedProfitLoss = holdings.Sum(item => item.UnrealizedProfitLoss)
        };
    }

    public async Task<TradingPortfolioPageViewModel> GetPortfolioAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(userId, cancellationToken);
        return new TradingPortfolioPageViewModel
        {
            Holdings = await GetHoldingsAsync(userId, cancellationToken),
            PriceWarning = await GetPriceWarningAsync(userId, cancellationToken)
        };
    }

    public async Task<TradingWatchlistPageViewModel> GetWatchlistAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(userId, cancellationToken);
        return new TradingWatchlistPageViewModel
        {
            Items = await GetWatchlistItemsAsync(userId, cancellationToken),
            PriceWarning = await GetPriceWarningAsync(userId, cancellationToken)
        };
    }

    public async Task<TradingJournalPageViewModel> GetJournalAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(userId, cancellationToken);
        return new TradingJournalPageViewModel
        {
            Entries = await GetJournalEntriesAsync(userId, cancellationToken)
        };
    }

    public async Task<TradingAdvisorPageViewModel> GetAdvisorAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(userId, cancellationToken);
        var history = await context.Set<TradingAdvisorSnapshot>()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        return new TradingAdvisorPageViewModel
        {
            LatestAdvice = history.Count == 0 ? null : MapAdvice(history[0]),
            History = history,
            PriceWarning = await GetPriceWarningAsync(userId, cancellationToken)
        };
    }

    public async Task<List<TradingHoldingViewModel>> GetHoldingsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(userId, cancellationToken);
        var holdings = await context.Set<TradingHolding>()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.Currency)
            .ThenBy(item => item.Symbol)
            .ToListAsync(cancellationToken);

        return holdings.Select(MapHolding).ToList();
    }

    public async Task<TradingHolding> SaveHoldingAsync(string userId, TradingHoldingInputModel model, CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(userId, cancellationToken);
        var entity = model.Id > 0
            ? await context.Set<TradingHolding>().FirstOrDefaultAsync(item => item.UserId == userId && item.Id == model.Id, cancellationToken)
            : null;

        if (entity == null)
        {
            entity = new TradingHolding
            {
                UserId = userId,
                CreatedAt = DateTime.Now
            };
            context.Add(entity);
        }

        entity.Symbol = model.Symbol.Trim().ToUpperInvariant();
        entity.Market = model.Market.Trim();
        entity.CompanyName = model.CompanyName.Trim();
        entity.Quantity = model.Quantity;
        entity.AverageCost = model.AverageCost;
        entity.Currency = model.Currency.Trim().ToUpperInvariant();
        entity.CurrentPrice = model.CurrentPrice ?? (entity.CurrentPrice > 0m ? entity.CurrentPrice : model.AverageCost);
        entity.Notes = model.Notes?.Trim();
        entity.UpdatedAt = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteHoldingAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Set<TradingHolding>().FirstOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
        if (entity == null)
        {
            return;
        }

        context.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TradingWatchlistItemViewModel>> GetWatchlistItemsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(userId, cancellationToken);
        var items = await context.Set<TradingWatchlistItem>()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.Symbol)
            .ToListAsync(cancellationToken);

        var prices = await GetLatestPriceMapAsync(userId, cancellationToken);
        return items.Select(item => MapWatchlist(item, prices)).ToList();
    }

    public async Task<TradingWatchlistItem> SaveWatchlistItemAsync(string userId, TradingWatchlistInputModel model, CancellationToken cancellationToken = default)
    {
        var entity = model.Id > 0
            ? await context.Set<TradingWatchlistItem>().FirstOrDefaultAsync(item => item.UserId == userId && item.Id == model.Id, cancellationToken)
            : null;

        if (entity == null)
        {
            entity = new TradingWatchlistItem
            {
                UserId = userId,
                CreatedAt = DateTime.Now
            };
            context.Add(entity);
        }

        entity.Symbol = model.Symbol.Trim().ToUpperInvariant();
        entity.Market = model.Market.Trim();
        entity.TargetBuyPrice = model.TargetBuyPrice;
        entity.TargetSellPrice = model.TargetSellPrice;
        entity.AlertBelowPrice = model.AlertBelowPrice;
        entity.AlertAbovePrice = model.AlertAbovePrice;
        entity.Notes = model.Notes?.Trim();
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteWatchlistItemAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Set<TradingWatchlistItem>().FirstOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
        if (entity == null)
        {
            return;
        }

        context.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TradingJournalEntry>> GetJournalEntriesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await context.Set<TradingJournalEntry>()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<TradingJournalEntry> AddJournalEntryAsync(string userId, TradingJournalEntryInputModel model, CancellationToken cancellationToken = default)
    {
        var entry = new TradingJournalEntry
        {
            UserId = userId,
            Symbol = model.Symbol.Trim().ToUpperInvariant(),
            ActionType = model.ActionType,
            Price = model.Price,
            Quantity = model.Quantity,
            Reason = model.Reason?.Trim(),
            Emotion = model.Emotion?.Trim(),
            LessonLearned = model.LessonLearned?.Trim(),
            CreatedAt = DateTime.Now
        };

        context.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<TradingAdvisorResultViewModel> GenerateAdviceAsync(string userId, bool saveToHistory = true, CancellationToken cancellationToken = default)
    {
        var dashboard = await GetDashboardAsync(userId, cancellationToken);
        var advice = await tradingAdvisorService.GenerateAsync(dashboard, cancellationToken);

        if (saveToHistory)
        {
            var snapshot = new TradingAdvisorSnapshot
            {
                UserId = userId,
                ProviderLabel = advice.ProviderLabel,
                ModelId = advice.ModelId,
                PortfolioSummary = advice.PortfolioSummary,
                RiskLevel = advice.RiskLevel,
                ConcentrationRisk = advice.ConcentrationRisk,
                PossibleNextAction = advice.PossibleNextAction,
                Suggestion = advice.Suggestion,
                Reasoning = advice.Reasoning,
                Disclaimer = advice.Disclaimer,
                RawJson = JsonSerializer.Serialize(advice),
                CreatedAt = advice.GeneratedAt
            };

            context.Add(snapshot);
            await context.SaveChangesAsync(cancellationToken);
        }

        return advice;
    }

    public async Task<List<TradingPriceResult>> RefreshPricesAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureSeedDataAsync(userId, cancellationToken);
        var holdings = await context.Set<TradingHolding>()
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);
        var watchlist = await context.Set<TradingWatchlistItem>()
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);

        var requestRows = holdings
            .Select(item => (item.Symbol, item.Market, item.Currency, item.CurrentPrice))
            .Concat(watchlist.Select(item => (item.Symbol, item.Market, InferCurrency(item.Symbol, item.Market), 0m)))
            .Distinct()
            .ToList();

        var refreshResult = await marketPriceService.RefreshAsync(userId, requestRows, cancellationToken);
        foreach (var price in refreshResult.Prices)
        {
            foreach (var holding in holdings.Where(item => item.Symbol == price.Symbol && item.Market == price.Market))
            {
                holding.CurrentPrice = price.Price;
                holding.UpdatedAt = DateTime.Now;
            }

            context.Add(new TradingPriceSnapshot
            {
                UserId = userId,
                Symbol = price.Symbol,
                Market = price.Market,
                Currency = price.Currency,
                Price = price.Price,
                Source = price.Source,
                IsLive = price.IsLive,
                CapturedAt = DateTime.Now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return refreshResult.Prices;
    }

    public async Task<string> GetPriceWarningAsync(string userId, CancellationToken cancellationToken = default)
    {
        var status = await marketPriceService.GetStatusAsync(cancellationToken);
        return status.HasConfiguredProvider ? status.LastTestSummary : status.WarningMessage;
    }

    private async Task EnsureSeedDataAsync(string userId, CancellationToken cancellationToken)
    {
        if (!await context.Set<TradingHolding>().AnyAsync(item => item.UserId == userId, cancellationToken))
        {
            context.AddRange(
                SeedHolding(userId, "AMC", "NYSE", "AMC Entertainment", 100m, 1.02m, "USD"),
                SeedHolding(userId, "NIO", "NYSE", "NIO", 46m, 41.72m, "USD"),
                SeedHolding(userId, "NTDOY", "OTC", "Nintendo", 100m, 19.01m, "USD"),
                SeedHolding(userId, "PINE", "NYSE", "Alpine Income Property Trust", 100m, 15.03m, "USD"),
                SeedHolding(userId, "RBLX", "NYSE", "Roblox", 10m, 91.72m, "USD"),
                SeedHolding(userId, "C38U", "SGX", "CapitaLand Integrated Commercial Trust", 100m, 2.012m, "SGD"),
                SeedHolding(userId, "T82U", "SGX", "Suntec REIT", 200m, 1.291m, "SGD"),
                SeedHolding(userId, "MSFT", "NASDAQ", "Microsoft", 3m, 428.24m, "USD"));
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static TradingHolding SeedHolding(string userId, string symbol, string market, string companyName, decimal quantity, decimal averageCost, string currency)
    {
        return new TradingHolding
        {
            UserId = userId,
            Symbol = symbol,
            Market = market,
            CompanyName = companyName,
            Quantity = quantity,
            AverageCost = averageCost,
            CurrentPrice = averageCost,
            Currency = currency,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    private static TradingHoldingViewModel MapHolding(TradingHolding item)
    {
        var totalCost = item.Quantity * item.AverageCost;
        var marketValue = item.Quantity * item.CurrentPrice;
        var pnl = marketValue - totalCost;

        return new TradingHoldingViewModel
        {
            Id = item.Id,
            Symbol = item.Symbol,
            Market = item.Market,
            CompanyName = item.CompanyName,
            Quantity = item.Quantity,
            AverageCost = item.AverageCost,
            Currency = item.Currency,
            CurrentPrice = item.CurrentPrice,
            TotalCost = totalCost,
            MarketValue = marketValue,
            UnrealizedProfitLoss = pnl,
            UnrealizedProfitLossPercent = totalCost == 0m ? 0m : pnl / totalCost * 100m,
            Notes = item.Notes,
            UpdatedAt = item.UpdatedAt
        };
    }

    private static TradingAdvisorResultViewModel MapAdvice(TradingAdvisorSnapshot snapshot)
    {
        return new TradingAdvisorResultViewModel
        {
            ProviderLabel = snapshot.ProviderLabel,
            ModelId = snapshot.ModelId,
            PortfolioSummary = snapshot.PortfolioSummary,
            RiskLevel = snapshot.RiskLevel,
            ConcentrationRisk = snapshot.ConcentrationRisk,
            PossibleNextAction = snapshot.PossibleNextAction,
            Suggestion = snapshot.Suggestion,
            Reasoning = snapshot.Reasoning,
            Disclaimer = snapshot.Disclaimer,
            UsedAi = !string.Equals(snapshot.ProviderLabel, "Fallback", StringComparison.OrdinalIgnoreCase),
            GeneratedAt = snapshot.CreatedAt
        };
    }

    private static TradingWatchlistItemViewModel MapWatchlist(TradingWatchlistItem item, IReadOnlyDictionary<string, TradingPriceSnapshot> prices)
    {
        prices.TryGetValue(BuildSymbolKey(item.Symbol, item.Market), out var snapshot);
        var currentPrice = snapshot?.Price;

        return new TradingWatchlistItemViewModel
        {
            Id = item.Id,
            Symbol = item.Symbol,
            Market = item.Market,
            CurrentPrice = currentPrice,
            TargetBuyPrice = item.TargetBuyPrice,
            TargetSellPrice = item.TargetSellPrice,
            AlertBelowPrice = item.AlertBelowPrice,
            AlertAbovePrice = item.AlertAbovePrice,
            IsActive = item.IsActive,
            AlertStatus = BuildAlertStatus(item, currentPrice),
            Notes = item.Notes,
            UpdatedAt = item.UpdatedAt
        };
    }

    private async Task<IReadOnlyDictionary<string, TradingPriceSnapshot>> GetLatestPriceMapAsync(string userId, CancellationToken cancellationToken)
    {
        var snapshots = await context.Set<TradingPriceSnapshot>()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CapturedAt)
            .ToListAsync(cancellationToken);

        return snapshots
            .GroupBy(item => BuildSymbolKey(item.Symbol, item.Market))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildAlertStatus(TradingWatchlistItem item, decimal? currentPrice)
    {
        if (!item.IsActive)
        {
            return "Inactive";
        }

        if (!currentPrice.HasValue)
        {
            return "No saved price";
        }

        if (item.AlertBelowPrice.HasValue && currentPrice.Value <= item.AlertBelowPrice.Value)
        {
            return $"Below alert {item.AlertBelowPrice.Value.ToString("0.####", CultureInfo.InvariantCulture)}";
        }

        if (item.AlertAbovePrice.HasValue && currentPrice.Value >= item.AlertAbovePrice.Value)
        {
            return $"Above alert {item.AlertAbovePrice.Value.ToString("0.####", CultureInfo.InvariantCulture)}";
        }

        if (item.TargetBuyPrice.HasValue && currentPrice.Value <= item.TargetBuyPrice.Value)
        {
            return "Near buy zone";
        }

        if (item.TargetSellPrice.HasValue && currentPrice.Value >= item.TargetSellPrice.Value)
        {
            return "Near sell zone";
        }

        return "Watching";
    }

    private static string InferCurrency(string symbol, string market)
    {
        return string.Equals(market, "SGX", StringComparison.OrdinalIgnoreCase)
            || symbol is "C38U" or "T82U"
            ? "SGD"
            : "USD";
    }

    private static string BuildSymbolKey(string symbol, string market) => $"{market}:{symbol}".ToUpperInvariant();
}

public class MarketPriceService(
    IMarketDataSettingsService marketDataSettingsService,
    IMarketDataProviderFactory marketDataProviderFactory,
    ILogger<MarketPriceService> logger) : IMarketPriceService
{
    public Task<(List<TradingPriceResult> Prices, string Warning)> RefreshAsync(
        string userId,
        IReadOnlyCollection<(string Symbol, string Market, string Currency, decimal SavedPrice)> requests,
        CancellationToken cancellationToken = default)
    {
        return RefreshCoreAsync(userId, requests, cancellationToken);
    }

    public Task<MarketDataProviderStatusViewModel> GetStatusAsync(CancellationToken cancellationToken = default)
        => marketDataSettingsService.GetStatusAsync(cancellationToken);

    private async Task<(List<TradingPriceResult> Prices, string Warning)> RefreshCoreAsync(
        string userId,
        IReadOnlyCollection<(string Symbol, string Market, string Currency, decimal SavedPrice)> requests,
        CancellationToken cancellationToken)
    {
        var status = await marketDataSettingsService.GetStatusAsync(cancellationToken);
        var providers = (await marketDataSettingsService.GetAllAsync(cancellationToken))
            .Where(item => item.IsEnabled)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.ProviderName)
            .ToList();

        if (providers.Count == 0)
        {
            logger.LogInformation("Trading price refresh requested for user {UserId}, but no live provider is configured.", userId);
            return (MapSavedPrices(requests), "Live price provider is not configured. Showing saved prices only.");
        }

        var prices = new List<TradingPriceResult>();
        foreach (var request in requests)
        {
            var result = await TryResolvePriceAsync(request, providers, cancellationToken);
            prices.Add(result);
        }

        return (prices, status.HasConfiguredProvider ? string.Empty : status.WarningMessage);
    }

    private async Task<TradingPriceResult> TryResolvePriceAsync(
        (string Symbol, string Market, string Currency, decimal SavedPrice) request,
        IReadOnlyList<MarketDataProviderSettings> providers,
        CancellationToken cancellationToken)
    {
        foreach (var settings in providers)
        {
            var provider = marketDataProviderFactory.Create(settings.ProviderName);
            try
            {
                var providerSymbol = provider.GetProviderSymbol(request.Symbol, request.Market, request.Currency);
                var quote = await provider.GetQuoteAsync(settings, request.Symbol, request.Market, request.Currency, cancellationToken);
                if (quote.HasValue)
                {
                    return new TradingPriceResult
                    {
                        Symbol = request.Symbol,
                        Market = request.Market,
                        Currency = request.Currency,
                        Price = quote.Value,
                        Source = settings.ProviderName.ToString(),
                        IsLive = true,
                        ProviderSymbol = providerSymbol
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Market quote refresh failed for provider {Provider} symbol {Symbol}.", settings.ProviderName, request.Symbol);
            }
        }

        return new TradingPriceResult
        {
            Symbol = request.Symbol,
            Market = request.Market,
            Currency = request.Currency,
            Price = request.SavedPrice > 0m ? request.SavedPrice : 0m,
            Source = "Saved",
            IsLive = false,
            FailureReason = "No enabled provider returned a quote for this symbol.",
            ProviderSymbol = BuildDisplaySymbol(request.Symbol, request.Market)
        };
    }

    private static List<TradingPriceResult> MapSavedPrices(IReadOnlyCollection<(string Symbol, string Market, string Currency, decimal SavedPrice)> requests)
    {
        return requests.Select(item => new TradingPriceResult
        {
            Symbol = item.Symbol,
            Market = item.Market,
            Currency = item.Currency,
            Price = item.SavedPrice > 0m ? item.SavedPrice : 0m,
            Source = "Saved",
            IsLive = false,
            FailureReason = "Live price provider is not configured.",
            ProviderSymbol = BuildDisplaySymbol(item.Symbol, item.Market)
        }).ToList();
    }

    private static string BuildDisplaySymbol(string symbol, string market) => $"{symbol}.{market}".ToUpperInvariant();
}

public class TradingAdvisorService(
    IAiProviderFactory aiProviderFactory,
    ILogger<TradingAdvisorService> logger) : ITradingAdvisorService
{
    private const string DisclaimerText = "This is AI-assisted analysis, not financial advice. Verify before making investment decisions.";

    public async Task<TradingAdvisorResultViewModel> GenerateAsync(TradingDashboardViewModel dashboard, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = await aiProviderFactory.CreateChatClientAsync(cancellationToken);
            var prompt = BuildPrompt(dashboard);
            var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)], cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(response.Text))
            {
                return BuildAiResult(response.Text.Trim(), dashboard);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Trading advisor AI generation failed. Falling back to deterministic summary.");
        }

        return BuildFallback(dashboard);
    }

    private static string BuildPrompt(TradingDashboardViewModel dashboard)
    {
        var holdings = string.Join(Environment.NewLine, dashboard.Holdings.Select(item =>
            $"- {item.Symbol} {item.Quantity} shares avg={item.AverageCost:0.####} current={item.CurrentPrice:0.####} pnl={item.UnrealizedProfitLoss:0.##} {item.Currency}"));
        var alerts = string.Join(Environment.NewLine, dashboard.Watchlist.Take(10).Select(item =>
            $"- {item.Symbol} {item.AlertStatus} current={item.CurrentPrice?.ToString("0.####") ?? "n/a"}"));

        return $"""
Create a structured portfolio advisory summary.
Rules:
- Advisory only.
- Do not recommend automatic trading.
- Do not mention guaranteed profit.
- Include the disclaimer exactly once: "{DisclaimerText}"
- Output valid JSON only.
- Use keys: portfolioSummary, riskLevel, concentrationRisk, possibleNextAction, suggestion, reasoning.

Portfolio totals:
- totalCost: {dashboard.TotalCost:0.##}
- totalMarketValue: {dashboard.TotalMarketValue:0.##}
- totalUnrealizedProfitLoss: {dashboard.TotalUnrealizedProfitLoss:0.##}
- totalUnrealizedProfitLossPercent: {dashboard.TotalUnrealizedProfitLossPercent:0.##}

Holdings:
{holdings}

Watchlist:
{alerts}
""";
    }

    private static TradingAdvisorResultViewModel BuildAiResult(string json, TradingDashboardViewModel dashboard)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new TradingAdvisorResultViewModel
        {
            ProviderLabel = "Ollama",
            PortfolioSummary = GetString(root, "portfolioSummary", BuildFallbackSummary(dashboard)),
            RiskLevel = GetString(root, "riskLevel", EstimateRiskLevel(dashboard)),
            ConcentrationRisk = GetString(root, "concentrationRisk", EstimateConcentrationRisk(dashboard)),
            PossibleNextAction = GetString(root, "possibleNextAction", "Review position sizing and saved buy/sell levels."),
            Suggestion = ParseSuggestion(GetString(root, "suggestion", "Hold")),
            Reasoning = GetString(root, "reasoning", BuildFallbackReasoning(dashboard)),
            Disclaimer = DisclaimerText,
            UsedAi = true,
            GeneratedAt = DateTime.Now
        };
    }

    private static TradingAdvisorResultViewModel BuildFallback(TradingDashboardViewModel dashboard)
    {
        return new TradingAdvisorResultViewModel
        {
            ProviderLabel = "Fallback",
            PortfolioSummary = BuildFallbackSummary(dashboard),
            RiskLevel = EstimateRiskLevel(dashboard),
            ConcentrationRisk = EstimateConcentrationRisk(dashboard),
            PossibleNextAction = "Review the largest position, confirm your target prices, and refresh saved prices before making any decision.",
            Suggestion = TradingAdvisorSuggestion.Hold,
            Reasoning = BuildFallbackReasoning(dashboard),
            Disclaimer = DisclaimerText,
            UsedAi = false,
            GeneratedAt = DateTime.Now
        };
    }

    private static string BuildFallbackSummary(TradingDashboardViewModel dashboard)
    {
        return $"Portfolio market value is {dashboard.TotalMarketValue:0.##} against cost {dashboard.TotalCost:0.##}, with unrealized P/L {dashboard.TotalUnrealizedProfitLoss:0.##}. {dashboard.Holdings.Count} holding(s) are being tracked and {dashboard.Watchlist.Count(item => item.AlertStatus != "Watching")} watchlist alert(s) are active.";
    }

    private static string EstimateRiskLevel(TradingDashboardViewModel dashboard)
    {
        var topWeight = GetTopWeight(dashboard);
        if (topWeight >= 45m)
        {
            return "High";
        }

        if (topWeight >= 25m)
        {
            return "Medium";
        }

        return "Moderate";
    }

    private static string EstimateConcentrationRisk(TradingDashboardViewModel dashboard)
    {
        var top = dashboard.Holdings.OrderByDescending(item => item.MarketValue).FirstOrDefault();
        if (top == null)
        {
            return "No holdings yet.";
        }

        var weight = GetTopWeight(dashboard);
        return $"{top.Symbol} is the largest holding at {weight:0.#}% of tracked market value.";
    }

    private static decimal GetTopWeight(TradingDashboardViewModel dashboard)
    {
        if (dashboard.TotalMarketValue <= 0m || dashboard.Holdings.Count == 0)
        {
            return 0m;
        }

        return dashboard.Holdings.Max(item => item.MarketValue) / dashboard.TotalMarketValue * 100m;
    }

    private static string BuildFallbackReasoning(TradingDashboardViewModel dashboard)
    {
        var top = dashboard.Holdings.OrderByDescending(item => item.MarketValue).FirstOrDefault();
        if (top == null)
        {
            return "No holdings are available yet, so the next step is to add positions and refresh saved prices.";
        }

        return $"The current view is based on saved prices, so position sizing is more reliable than short-term signals right now. {top.Symbol} is the largest holding, so review whether its weight still matches your risk tolerance before adding exposure elsewhere.";
    }

    private static string GetString(JsonElement root, string propertyName, string fallback)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static TradingAdvisorSuggestion ParseSuggestion(string value)
    {
        return Enum.TryParse<TradingAdvisorSuggestion>(value, true, out var suggestion)
            ? suggestion
            : TradingAdvisorSuggestion.Hold;
    }
}
