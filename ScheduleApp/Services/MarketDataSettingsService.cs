using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IMarketDataSettingsService
{
    Task<MarketDataSettingsInputViewModel> GetInputAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(MarketDataSettingsInputViewModel model, CancellationToken cancellationToken = default);
    Task<MarketDataProviderTestResultViewModel> TestAsync(MarketDataProviderName providerName, CancellationToken cancellationToken = default);
    Task<MarketDataProviderSettings?> GetPrimaryProviderAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketDataProviderSettings>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<MarketDataProviderStatusViewModel> GetStatusAsync(CancellationToken cancellationToken = default);
}

public interface IMarketDataProviderFactory
{
    Task<IMarketDataProviderClient> CreatePrimaryAsync(CancellationToken cancellationToken = default);
    IMarketDataProviderClient Create(MarketDataProviderName providerName);
}

public interface IMarketDataProviderClient
{
    MarketDataProviderName ProviderName { get; }
    bool SupportsApiKey { get; }
    bool IsExperimental { get; }
    string GetProviderSymbol(string symbol, string market, string currency);
    Task<MarketDataProviderTestResultViewModel> TestAsync(MarketDataProviderSettings settings, CancellationToken cancellationToken = default);
    Task<decimal?> GetQuoteAsync(MarketDataProviderSettings settings, string symbol, string market, string currency, CancellationToken cancellationToken = default);
}

public class MarketDataSettingsService(
    AppDbContext context,
    IDataProtectionProvider dataProtectionProvider,
    IHttpClientFactory httpClientFactory) : IMarketDataSettingsService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("ScheduleApp.MarketData.ApiKeys");

    public async Task<MarketDataSettingsInputViewModel> GetInputAsync(CancellationToken cancellationToken = default)
    {
        var entities = await GetOrCreateDefaultsAsync(cancellationToken);
        return new MarketDataSettingsInputViewModel
        {
            Providers = entities
                .OrderBy(item => item.ProviderName)
                .Select(MapInput)
                .ToList()
        };
    }

    public async Task SaveAsync(MarketDataSettingsInputViewModel model, CancellationToken cancellationToken = default)
    {
        var entities = await GetOrCreateDefaultsAsync(cancellationToken);
        var entityMap = entities.ToDictionary(item => item.ProviderName);
        var requestedPrimary = model.Providers.FirstOrDefault(item => item.IsPrimary)?.ProviderName;

        foreach (var input in model.Providers)
        {
            if (!entityMap.TryGetValue(input.ProviderName, out var entity))
            {
                continue;
            }

            entity.IsEnabled = input.IsEnabled;
            entity.IsPrimary = requestedPrimary.HasValue && requestedPrimary.Value == entity.ProviderName;
            entity.Notes = input.Notes?.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            if (input.ClearApiKey)
            {
                entity.ApiKeyEncrypted = null;
            }
            else if (!string.IsNullOrWhiteSpace(input.ApiKey) && !string.Equals(input.ApiKey, "configured", StringComparison.Ordinal))
            {
                entity.ApiKeyEncrypted = _protector.Protect(input.ApiKey.Trim());
            }
        }

        if (!entities.Any(item => item.IsPrimary && item.IsEnabled))
        {
            var mock = entities.First(item => item.ProviderName == MarketDataProviderName.MockData);
            mock.IsPrimary = true;
            mock.IsEnabled = true;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<MarketDataProviderTestResultViewModel> TestAsync(MarketDataProviderName providerName, CancellationToken cancellationToken = default)
    {
        var entities = await GetOrCreateDefaultsAsync(cancellationToken);
        var entity = entities.First(item => item.ProviderName == providerName);
        entity.ApiKey = Decrypt(entity.ApiKeyEncrypted);

        var provider = CreateProvider(providerName);
        var result = await provider.TestAsync(entity, cancellationToken);
        entity.LastTestResult = result.Message;
        entity.LastTestDate = result.TestedAtUtc;
        entity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<MarketDataProviderSettings?> GetPrimaryProviderAsync(CancellationToken cancellationToken = default)
    {
        var entities = await GetOrCreateDefaultsAsync(cancellationToken);
        var primary = entities.FirstOrDefault(item => item.IsPrimary && item.IsEnabled);
        if (primary == null)
        {
            return null;
        }

        primary.ApiKey = Decrypt(primary.ApiKeyEncrypted);
        return primary;
    }

    public async Task<IReadOnlyList<MarketDataProviderSettings>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await GetOrCreateDefaultsAsync(cancellationToken);
        foreach (var entity in entities)
        {
            entity.ApiKey = Decrypt(entity.ApiKeyEncrypted);
        }

        return entities;
    }

    public async Task<MarketDataProviderStatusViewModel> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var primary = await GetPrimaryProviderAsync(cancellationToken);
        if (primary == null)
        {
            return new MarketDataProviderStatusViewModel
            {
                HasConfiguredProvider = false,
                WarningMessage = "Live price provider is not configured. Showing saved prices only.",
                Status = "Not configured"
            };
        }

        var label = GetProviderLabel(primary.ProviderName);
        return new MarketDataProviderStatusViewModel
        {
            HasConfiguredProvider = true,
            ProviderLabel = label,
            Status = !string.IsNullOrWhiteSpace(primary.LastTestResult) && primary.LastTestDate.HasValue ? "Connected" : "Configured",
            WarningMessage = string.Empty,
            LastTestSummary = primary.LastTestDate.HasValue
                ? $"Provider: {label} | Status: {(!string.IsNullOrWhiteSpace(primary.LastTestResult) ? "Connected" : "Configured")} | Last Test: {primary.LastTestDate.Value.ToLocalTime():yyyy-MM-dd HH:mm}"
                : $"Provider: {label} | Status: Configured",
            LastTestDateUtc = primary.LastTestDate
        };
    }

    private async Task<List<MarketDataProviderSettings>> GetOrCreateDefaultsAsync(CancellationToken cancellationToken)
    {
        var existing = await context.Set<MarketDataProviderSettings>().OrderBy(item => item.Id).ToListAsync(cancellationToken);
        if (existing.Count == 0)
        {
            existing =
            [
                CreateDefault(MarketDataProviderName.MockData, true, true),
                CreateDefault(MarketDataProviderName.AlphaVantage, false, false),
                CreateDefault(MarketDataProviderName.Finnhub, false, false),
                CreateDefault(MarketDataProviderName.YahooFinance, false, false),
                CreateDefault(MarketDataProviderName.CoinGecko, false, false)
            ];
            context.AddRange(existing);
            await context.SaveChangesAsync(cancellationToken);
        }

        return existing;
    }

    private MarketDataProviderInputItemViewModel MapInput(MarketDataProviderSettings entity)
    {
        var provider = CreateProvider(entity.ProviderName);
        var apiKey = Decrypt(entity.ApiKeyEncrypted);
        return new MarketDataProviderInputItemViewModel
        {
            Id = entity.Id,
            ProviderName = entity.ProviderName,
            ProviderLabel = GetProviderLabel(entity.ProviderName),
            IsEnabled = entity.IsEnabled,
            IsPrimary = entity.IsPrimary,
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : "configured",
            HasApiKey = !string.IsNullOrWhiteSpace(apiKey),
            MaskedApiKey = MaskKey(apiKey),
            Notes = entity.Notes,
            LastTestResult = entity.LastTestResult,
            LastTestDate = entity.LastTestDate,
            Status = BuildStatus(entity),
            SupportsApiKey = provider.SupportsApiKey,
            IsExperimental = provider.IsExperimental
        };
    }

    private string? Decrypt(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(encrypted);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildStatus(MarketDataProviderSettings entity)
    {
        if (!entity.IsEnabled)
        {
            return "Disabled";
        }

        if (entity.LastTestDate.HasValue && !string.IsNullOrWhiteSpace(entity.LastTestResult))
        {
            return entity.LastTestResult.Contains("success", StringComparison.OrdinalIgnoreCase)
                || entity.LastTestResult.Contains("connected", StringComparison.OrdinalIgnoreCase)
                ? "Connected"
                : "Configured";
        }

        return entity.IsPrimary ? "Primary" : "Configured";
    }

    private static string GetProviderLabel(MarketDataProviderName providerName) => providerName switch
    {
        MarketDataProviderName.MockData => "Mock Data",
        MarketDataProviderName.AlphaVantage => "Alpha Vantage",
        MarketDataProviderName.Finnhub => "Finnhub",
        MarketDataProviderName.YahooFinance => "Yahoo Finance (Experimental)",
        MarketDataProviderName.CoinGecko => "CoinGecko (Crypto)",
        _ => providerName.ToString()
    };

    private static string MaskKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Not configured";
        }

        var trimmed = value.Trim();
        var suffix = trimmed.Length <= 6 ? trimmed : trimmed[^6..];
        return $"********{suffix}";
    }

    private static MarketDataProviderSettings CreateDefault(MarketDataProviderName name, bool isEnabled, bool isPrimary)
    {
        return new MarketDataProviderSettings
        {
            ProviderName = name,
            IsEnabled = isEnabled,
            IsPrimary = isPrimary,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private IMarketDataProviderClient CreateProvider(MarketDataProviderName providerName) => providerName switch
    {
        MarketDataProviderName.AlphaVantage => new AlphaVantageMarketDataProvider(httpClientFactory.CreateClient()),
        MarketDataProviderName.Finnhub => new FinnhubMarketDataProvider(httpClientFactory.CreateClient()),
        MarketDataProviderName.YahooFinance => new YahooFinanceMarketDataProvider(httpClientFactory.CreateClient()),
        MarketDataProviderName.CoinGecko => new CoinGeckoMarketDataProvider(httpClientFactory.CreateClient()),
        _ => new MockMarketDataProvider()
    };
}

public class MarketDataProviderFactory(
    IHttpClientFactory httpClientFactory,
    IMarketDataSettingsService marketDataSettingsService) : IMarketDataProviderFactory
{
    public async Task<IMarketDataProviderClient> CreatePrimaryAsync(CancellationToken cancellationToken = default)
    {
        var primary = await marketDataSettingsService.GetPrimaryProviderAsync(cancellationToken);
        return Create(primary?.ProviderName ?? MarketDataProviderName.MockData);
    }

    public IMarketDataProviderClient Create(MarketDataProviderName providerName) => providerName switch
    {
        MarketDataProviderName.AlphaVantage => new AlphaVantageMarketDataProvider(httpClientFactory.CreateClient()),
        MarketDataProviderName.Finnhub => new FinnhubMarketDataProvider(httpClientFactory.CreateClient()),
        MarketDataProviderName.YahooFinance => new YahooFinanceMarketDataProvider(httpClientFactory.CreateClient()),
        MarketDataProviderName.CoinGecko => new CoinGeckoMarketDataProvider(httpClientFactory.CreateClient()),
        _ => new MockMarketDataProvider()
    };
}

file sealed class MockMarketDataProvider : IMarketDataProviderClient
{
    public MarketDataProviderName ProviderName => MarketDataProviderName.MockData;
    public bool SupportsApiKey => false;
    public bool IsExperimental => false;
    public string GetProviderSymbol(string symbol, string market, string currency) => symbol;

    public Task<MarketDataProviderTestResultViewModel> TestAsync(MarketDataProviderSettings settings, CancellationToken cancellationToken = default)
        => Task.FromResult(new MarketDataProviderTestResultViewModel
        {
            ProviderName = ProviderName,
            Success = true,
            Status = "Connected",
            Message = "MSFT quote received successfully.",
            QuotePrice = 428.24m
        });

    public Task<decimal?> GetQuoteAsync(MarketDataProviderSettings settings, string symbol, string market, string currency, CancellationToken cancellationToken = default)
        => Task.FromResult<decimal?>(symbol.Equals("MSFT", StringComparison.OrdinalIgnoreCase) ? 428.24m : null);
}

file sealed class AlphaVantageMarketDataProvider(HttpClient client) : IMarketDataProviderClient
{
    public MarketDataProviderName ProviderName => MarketDataProviderName.AlphaVantage;
    public bool SupportsApiKey => true;
    public bool IsExperimental => false;
    public string GetProviderSymbol(string symbol, string market, string currency) => MarketSymbolMapper.ToAlphaVantage(symbol, market);

    public async Task<MarketDataProviderTestResultViewModel> TestAsync(MarketDataProviderSettings settings, CancellationToken cancellationToken = default)
    {
        var quote = await GetQuoteAsync(settings, "MSFT", "NASDAQ", "USD", cancellationToken);
        return new MarketDataProviderTestResultViewModel
        {
            ProviderName = ProviderName,
            Success = quote.HasValue,
            Status = quote.HasValue ? "Connected" : "Error",
            Message = quote.HasValue ? "MSFT quote received successfully." : "Invalid API key.",
            QuotePrice = quote
        };
    }

    public async Task<decimal?> GetQuoteAsync(MarketDataProviderSettings settings, string symbol, string market, string currency, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return null;
        }

        var providerSymbol = GetProviderSymbol(symbol, market, currency);
        var url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={Uri.EscapeDataString(providerSymbol)}&apikey={Uri.EscapeDataString(settings.ApiKey)}";
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (doc.RootElement.TryGetProperty("Global Quote", out var quote)
            && quote.TryGetProperty("05. price", out var priceNode)
            && decimal.TryParse(priceNode.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
        {
            return price;
        }

        return null;
    }
}

file sealed class FinnhubMarketDataProvider(HttpClient client) : IMarketDataProviderClient
{
    public MarketDataProviderName ProviderName => MarketDataProviderName.Finnhub;
    public bool SupportsApiKey => true;
    public bool IsExperimental => false;
    public string GetProviderSymbol(string symbol, string market, string currency) => MarketSymbolMapper.ToFinnhub(symbol, market);

    public async Task<MarketDataProviderTestResultViewModel> TestAsync(MarketDataProviderSettings settings, CancellationToken cancellationToken = default)
    {
        var quote = await GetQuoteAsync(settings, "MSFT", "NASDAQ", "USD", cancellationToken);
        return new MarketDataProviderTestResultViewModel
        {
            ProviderName = ProviderName,
            Success = quote.HasValue,
            Status = quote.HasValue ? "Connected" : "Error",
            Message = quote.HasValue ? "MSFT quote received successfully." : "Invalid API key.",
            QuotePrice = quote
        };
    }

    public async Task<decimal?> GetQuoteAsync(MarketDataProviderSettings settings, string symbol, string market, string currency, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return null;
        }

        var providerSymbol = GetProviderSymbol(symbol, market, currency);
        var url = $"https://finnhub.io/api/v1/quote?symbol={Uri.EscapeDataString(providerSymbol)}&token={Uri.EscapeDataString(settings.ApiKey)}";
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (doc.RootElement.TryGetProperty("c", out var currentNode) && currentNode.TryGetDecimal(out var current))
        {
            return current > 0m ? current : null;
        }

        return null;
    }
}

file sealed class YahooFinanceMarketDataProvider(HttpClient client) : IMarketDataProviderClient
{
    public MarketDataProviderName ProviderName => MarketDataProviderName.YahooFinance;
    public bool SupportsApiKey => false;
    public bool IsExperimental => true;
    public string GetProviderSymbol(string symbol, string market, string currency) => MarketSymbolMapper.ToYahoo(symbol, market);

    public async Task<MarketDataProviderTestResultViewModel> TestAsync(MarketDataProviderSettings settings, CancellationToken cancellationToken = default)
    {
        var quote = await GetQuoteAsync(settings, "MSFT", "NASDAQ", "USD", cancellationToken);
        return new MarketDataProviderTestResultViewModel
        {
            ProviderName = ProviderName,
            Success = quote.HasValue,
            Status = quote.HasValue ? "Connected" : "Error",
            Message = quote.HasValue ? "MSFT quote received successfully." : "Yahoo Finance test failed.",
            QuotePrice = quote
        };
    }

    public async Task<decimal?> GetQuoteAsync(MarketDataProviderSettings settings, string symbol, string market, string currency, CancellationToken cancellationToken = default)
    {
        var providerSymbol = GetProviderSymbol(symbol, market, currency);
        var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={Uri.EscapeDataString(providerSymbol)}";
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var result = doc.RootElement
            .GetProperty("quoteResponse")
            .GetProperty("result")
            .EnumerateArray()
            .FirstOrDefault();

        if (result.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (result.TryGetProperty("regularMarketPrice", out var priceNode) && priceNode.TryGetDecimal(out var price))
        {
            return price;
        }

        return null;
    }
}

file sealed class CoinGeckoMarketDataProvider(HttpClient client) : IMarketDataProviderClient
{
    public MarketDataProviderName ProviderName => MarketDataProviderName.CoinGecko;
    public bool SupportsApiKey => false;
    public bool IsExperimental => false;
    public string GetProviderSymbol(string symbol, string market, string currency) => MarketSymbolMapper.ToCoinGecko(symbol);

    public async Task<MarketDataProviderTestResultViewModel> TestAsync(MarketDataProviderSettings settings, CancellationToken cancellationToken = default)
    {
        var price = await GetQuoteAsync(settings, "MSFT", "NASDAQ", "USD", cancellationToken);
        return new MarketDataProviderTestResultViewModel
        {
            ProviderName = ProviderName,
            Success = price.HasValue,
            Status = price.HasValue ? "Connected" : "Connected",
            Message = price.HasValue
                ? "Provider reachable. CoinGecko is crypto-only; test used BTC/USD connectivity."
                : "CoinGecko connectivity test failed.",
            QuotePrice = price
        };
    }

    public async Task<decimal?> GetQuoteAsync(MarketDataProviderSettings settings, string symbol, string market, string currency, CancellationToken cancellationToken = default)
    {
        var targetCurrency = string.Equals(currency, "SGD", StringComparison.OrdinalIgnoreCase) ? "sgd" : "usd";
        var coinId = GetProviderSymbol(symbol, market, currency);
        var url = $"https://api.coingecko.com/api/v3/simple/price?ids={Uri.EscapeDataString(coinId)}&vs_currencies={Uri.EscapeDataString(targetCurrency)}";
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (doc.RootElement.TryGetProperty(coinId, out var coinNode)
            && coinNode.TryGetProperty(targetCurrency, out var priceNode)
            && priceNode.TryGetDecimal(out var price))
        {
            return price;
        }

        return null;
    }
}

file static class MarketSymbolMapper
{
    public static string ToYahoo(string symbol, string market)
    {
        if (string.Equals(market, "SGX", StringComparison.OrdinalIgnoreCase))
        {
            return $"{symbol}.SI";
        }

        return symbol.ToUpperInvariant();
    }

    public static string ToFinnhub(string symbol, string market)
    {
        if (string.Equals(market, "SGX", StringComparison.OrdinalIgnoreCase))
        {
            return $"SGX:{symbol.ToUpperInvariant()}";
        }

        return symbol.ToUpperInvariant();
    }

    public static string ToAlphaVantage(string symbol, string market)
    {
        return string.Equals(market, "SGX", StringComparison.OrdinalIgnoreCase)
            ? $"{symbol}.SG"
            : symbol.ToUpperInvariant();
    }

    public static string ToCoinGecko(string symbol)
    {
        return symbol.Equals("BTC", StringComparison.OrdinalIgnoreCase) ? "bitcoin" :
            symbol.Equals("ETH", StringComparison.OrdinalIgnoreCase) ? "ethereum" :
            symbol.ToLowerInvariant();
    }
}
