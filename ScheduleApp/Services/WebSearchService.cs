using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IWebSearchService
{
    Task<WebSearchResponse> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default);
}

public class SearxngWebSearchService(
    HttpClient httpClient,
    IZeroAssistantDataService zeroAssistantDataService,
    ILogger<SearxngWebSearchService> logger) : IWebSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Description("Search the public web using local SearXNG for current information, recent events, product info, software updates, documentation, and general web lookup.")]
    public async Task<WebSearchResponse> SearchAsync(
        [Description("The public web query to look up, such as latest product news, current pricing, public documentation, or website information.")] string query,
        [Description("Maximum number of results to return. Use a small number like 3 to 5 unless the user asks for more.")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var resultLimit = Math.Clamp(maxResults, 1, 10);
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return new WebSearchResponse
            {
                Query = normalizedQuery,
                Success = false,
                ErrorMessage = "A search query is required."
            };
        }

        try
        {
            var settings = await zeroAssistantDataService.GetSettingsAsync(cancellationToken);
            var baseUrl = string.IsNullOrWhiteSpace(settings.SearXngBaseUrl)
                ? "http://localhost:10100"
                : settings.SearXngBaseUrl.Trim().TrimEnd('/');
            var endpoint = $"{baseUrl}/search?q={Uri.EscapeDataString(normalizedQuery)}&format=json";

            httpClient.Timeout = TimeSpan.FromSeconds(Math.Min(settings.RequestTimeoutSeconds, 20));
            using var response = await httpClient.GetAsync(endpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                logger.LogWarning(
                    "SearXNG search failed. Query={Query} StatusCode={StatusCode} ElapsedMs={ElapsedMs}",
                    normalizedQuery,
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);

                return new WebSearchResponse
                {
                    Query = normalizedQuery,
                    Success = false,
                    ErrorMessage = $"Search service returned HTTP {(int)response.StatusCode}.",
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var items = ParseResults(document.RootElement, resultLimit);
            stopwatch.Stop();

            logger.LogInformation(
                "SearXNG search completed. Query={Query} ResultCount={ResultCount} ElapsedMs={ElapsedMs}",
                normalizedQuery,
                items.Count,
                stopwatch.ElapsedMilliseconds);

            return new WebSearchResponse
            {
                Query = normalizedQuery,
                Results = items,
                Success = items.Count > 0,
                ErrorMessage = items.Count == 0 ? "No public web results were returned." : null,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            logger.LogWarning(
                "SearXNG search timed out. Query={Query} ElapsedMs={ElapsedMs}",
                normalizedQuery,
                stopwatch.ElapsedMilliseconds);

            return new WebSearchResponse
            {
                Query = normalizedQuery,
                Success = false,
                ErrorMessage = "Search request timed out.",
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            logger.LogWarning(
                ex,
                "SearXNG search HTTP error. Query={Query} ElapsedMs={ElapsedMs}",
                normalizedQuery,
                stopwatch.ElapsedMilliseconds);

            return new WebSearchResponse
            {
                Query = normalizedQuery,
                Success = false,
                ErrorMessage = "Search service is unavailable.",
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            logger.LogWarning(
                ex,
                "SearXNG search returned invalid JSON. Query={Query} ElapsedMs={ElapsedMs}",
                normalizedQuery,
                stopwatch.ElapsedMilliseconds);

            return new WebSearchResponse
            {
                Query = normalizedQuery,
                Success = false,
                ErrorMessage = "Search service returned invalid JSON.",
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "SearXNG search failed unexpectedly. Query={Query} ElapsedMs={ElapsedMs}",
                normalizedQuery,
                stopwatch.ElapsedMilliseconds);

            return new WebSearchResponse
            {
                Query = normalizedQuery,
                Success = false,
                ErrorMessage = ex.Message,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private static IReadOnlyList<WebSearchResult> ParseResults(JsonElement root, int maxResults)
    {
        if (!root.TryGetProperty("results", out var resultsElement) || resultsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<WebSearchResult>(maxResults);
        foreach (var item in resultsElement.EnumerateArray())
        {
            if (items.Count >= maxResults)
            {
                break;
            }

            var title = GetString(item, "title");
            var url = GetString(item, "url");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            items.Add(new WebSearchResult
            {
                Title = title,
                Url = url,
                Snippet = GetString(item, "content") ?? GetString(item, "snippet") ?? string.Empty,
                Source = GetString(item, "engine") ?? GetString(item, "parsed_url") ?? string.Empty
            });
        }

        return items;
    }

    private static string? GetString(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ValueKind == JsonValueKind.Array
                ? string.Join(", ", property.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()))
                : property.ValueKind == JsonValueKind.Object
                    ? JsonSerializer.Serialize(property, JsonOptions)
                    : property.ToString();
    }
}
