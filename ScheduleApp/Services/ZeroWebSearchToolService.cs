using System.ComponentModel;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IZeroWebSearchToolService
{
    Task<WebSearchResponse> web_search(string query, int maxResults = 5, CancellationToken cancellationToken = default);
}

public class ZeroWebSearchToolService(IWebSearchService webSearchService) : IZeroWebSearchToolService
{
    [Description("Search the public web using local SearXNG for current information, recent events, product info, software updates, documentation, and general web lookup.")]
    public Task<WebSearchResponse> web_search(
        [Description("The public web search query.")] string query,
        [Description("Maximum number of results to return.")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        return webSearchService.SearchAsync(query, maxResults, cancellationToken);
    }
}
