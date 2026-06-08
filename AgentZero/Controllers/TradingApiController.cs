using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Modules.Trading;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

[Authorize]
[ApiController]
[Route("api/trading")]
public class TradingApiController(ITradingService tradingService, ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet("portfolio")]
    public Task<List<TradingHoldingViewModel>> GetPortfolio(CancellationToken cancellationToken) =>
        tradingService.GetHoldingsAsync(currentUserService.UserId, cancellationToken);

    [HttpPost("portfolio")]
    public Task<TradingHolding> PostHolding([FromBody] TradingHoldingInputModel model, CancellationToken cancellationToken) =>
        tradingService.SaveHoldingAsync(currentUserService.UserId, model, cancellationToken);

    [HttpPut("portfolio")]
    public Task<TradingHolding> PutHolding([FromBody] TradingHoldingInputModel model, CancellationToken cancellationToken) =>
        tradingService.SaveHoldingAsync(currentUserService.UserId, model, cancellationToken);

    [HttpDelete("portfolio/{id:int}")]
    public async Task<IActionResult> DeleteHolding(int id, CancellationToken cancellationToken)
    {
        await tradingService.DeleteHoldingAsync(currentUserService.UserId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("prices/refresh-prices")]
    public Task<List<TradingPriceResult>> RefreshPrices(CancellationToken cancellationToken) =>
        tradingService.RefreshPricesAsync(currentUserService.UserId, cancellationToken);

    [HttpGet("prices")]
    public Task<string> GetPriceWarning(CancellationToken cancellationToken) =>
        tradingService.GetPriceWarningAsync(currentUserService.UserId, cancellationToken);

    [HttpGet("watchlist")]
    public Task<List<TradingWatchlistItemViewModel>> GetWatchlist(CancellationToken cancellationToken) =>
        tradingService.GetWatchlistItemsAsync(currentUserService.UserId, cancellationToken);

    [HttpPost("watchlist")]
    public Task<TradingWatchlistItem> PostWatchlist([FromBody] TradingWatchlistInputModel model, CancellationToken cancellationToken) =>
        tradingService.SaveWatchlistItemAsync(currentUserService.UserId, model, cancellationToken);

    [HttpPut("watchlist")]
    public Task<TradingWatchlistItem> PutWatchlist([FromBody] TradingWatchlistInputModel model, CancellationToken cancellationToken) =>
        tradingService.SaveWatchlistItemAsync(currentUserService.UserId, model, cancellationToken);

    [HttpDelete("watchlist/{id:int}")]
    public async Task<IActionResult> DeleteWatchlist(int id, CancellationToken cancellationToken)
    {
        await tradingService.DeleteWatchlistItemAsync(currentUserService.UserId, id, cancellationToken);
        return NoContent();
    }

    [HttpGet("journal")]
    public Task<List<TradingJournalEntry>> GetJournal(CancellationToken cancellationToken) =>
        tradingService.GetJournalEntriesAsync(currentUserService.UserId, cancellationToken);

    [HttpPost("journal")]
    public Task<TradingJournalEntry> PostJournal([FromBody] TradingJournalEntryInputModel model, CancellationToken cancellationToken) =>
        tradingService.AddJournalEntryAsync(currentUserService.UserId, model, cancellationToken);

    [HttpPost("advisor/generate-advice")]
    public Task<TradingAdvisorResultViewModel> GenerateAdvice(CancellationToken cancellationToken) =>
        tradingService.GenerateAdviceAsync(currentUserService.UserId, true, cancellationToken);

    [HttpGet("advisor")]
    public Task<TradingAdvisorPageViewModel> GetAdvisor(CancellationToken cancellationToken) =>
        tradingService.GetAdvisorAsync(currentUserService.UserId, cancellationToken);
}
