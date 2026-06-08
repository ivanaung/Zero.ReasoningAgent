using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

public class SearchController(IWebSearchService webSearchService) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Search";
        return View();
    }

    [AllowAnonymous]
    [HttpGet("/api/search/test")]
    public async Task<IActionResult> Test([FromQuery] string q, [FromQuery] int maxResults = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error = "Query parameter q is required." });
        }

        return Json(await webSearchService.SearchAsync(q, maxResults, cancellationToken));
    }
}
