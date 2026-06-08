namespace ScheduleApp.Models;

public class WebSearchResult
{
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Snippet { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}

public class WebSearchResponse
{
    public string Query { get; set; } = string.Empty;

    public IReadOnlyList<WebSearchResult> Results { get; set; } = [];

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public long ElapsedMilliseconds { get; set; }
}
