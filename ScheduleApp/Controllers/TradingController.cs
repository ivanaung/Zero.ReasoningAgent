using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Modules.Trading;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

[Authorize]
[Route("trading")]
public class TradingController(ITradingService tradingService, ICurrentUserService currentUserService) : Controller
{
    [HttpGet("")]
    public IActionResult Root() => RedirectToAction(nameof(Index));

    [HttpGet("dashboard")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Trading Dashboard";
        return View(await tradingService.GetDashboardAsync(currentUserService.UserId, cancellationToken));
    }

    [HttpGet("portfolio")]
    public async Task<IActionResult> Portfolio(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Trading Portfolio";
        return View(await tradingService.GetPortfolioAsync(currentUserService.UserId, cancellationToken));
    }

    [HttpPost("portfolio")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveHolding(TradingHoldingInputModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Portfolio));
        }

        await tradingService.SaveHoldingAsync(currentUserService.UserId, model, cancellationToken);
        return RedirectToAction(nameof(Portfolio));
    }

    [HttpPost("portfolio/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHolding(int id, CancellationToken cancellationToken)
    {
        await tradingService.DeleteHoldingAsync(currentUserService.UserId, id, cancellationToken);
        return RedirectToAction(nameof(Portfolio));
    }

    [HttpPost("portfolio/refresh-prices")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshPrices(CancellationToken cancellationToken)
    {
        var results = await tradingService.RefreshPricesAsync(currentUserService.UserId, cancellationToken);
        var liveCount = results.Count(item => item.IsLive);
        var failedSymbols = results
            .Where(item => !item.IsLive)
            .Select(item => string.IsNullOrWhiteSpace(item.ProviderSymbol) ? $"{item.Symbol} ({item.Market})" : $"{item.Symbol} ({item.Market} -> {item.ProviderSymbol})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        TempData["TradingRefreshSummary"] = failedSymbols.Count == 0
            ? $"Price refresh completed. Updated {liveCount} symbol(s)."
            : $"Price refresh updated {liveCount} symbol(s). Still using saved prices for: {string.Join(", ", failedSymbols)}.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("watchlist")]
    public async Task<IActionResult> Watchlist(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Trading Watchlist";
        return View(await tradingService.GetWatchlistAsync(currentUserService.UserId, cancellationToken));
    }

    [HttpPost("watchlist")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveWatchlistItem(TradingWatchlistInputModel model, CancellationToken cancellationToken)
    {
        await tradingService.SaveWatchlistItemAsync(currentUserService.UserId, model, cancellationToken);
        return RedirectToAction(nameof(Watchlist));
    }

    [HttpPost("watchlist/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteWatchlistItem(int id, CancellationToken cancellationToken)
    {
        await tradingService.DeleteWatchlistItemAsync(currentUserService.UserId, id, cancellationToken);
        return RedirectToAction(nameof(Watchlist));
    }

    [HttpGet("journal")]
    public async Task<IActionResult> Journal(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Trading Journal";
        return View(await tradingService.GetJournalAsync(currentUserService.UserId, cancellationToken));
    }

    [HttpPost("journal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddJournalEntry(TradingJournalEntryInputModel model, CancellationToken cancellationToken)
    {
        await tradingService.AddJournalEntryAsync(currentUserService.UserId, model, cancellationToken);
        return RedirectToAction(nameof(Journal));
    }

    [HttpGet("advisor")]
    public async Task<IActionResult> Advisor(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Trading Advisor";
        return View(await tradingService.GetAdvisorAsync(currentUserService.UserId, cancellationToken));
    }

    [HttpPost("advisor/generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateAdvice(CancellationToken cancellationToken)
    {
        await tradingService.GenerateAdviceAsync(currentUserService.UserId, true, cancellationToken);
        return RedirectToAction(nameof(Advisor));
    }
}
