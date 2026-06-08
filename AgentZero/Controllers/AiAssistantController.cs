using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Models.ViewModels;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

[Route("ai")]
public class AiAssistantController(
    IAiSettingsService aiSettingsService,
    IAiProviderFactory aiProviderFactory,
    IZeroAssistantService zeroAssistantService,
    IProjectManagementAgentService projectManagementAgentService,
    IProjectMonitoringService projectMonitoringService,
    IAiAuditService aiAuditService,
    ILogger<AiAssistantController> logger) : Controller
{
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        try
        {
            var response = await zeroAssistantService.SendTextAsync(request, cancellationToken);
            return Json(new AiChatResponse
            {
                ConversationId = response.ConversationId,
                Message = response.ReplyText,
                ProviderLabel = response.ProviderLabel,
                ModelId = response.ModelId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpGet("stream")]
    public async Task Stream([FromQuery] string message, [FromQuery] string? conversationId, CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";

        try
        {
            await foreach (var chunk in projectManagementAgentService.StreamAsync(new AiChatRequest
            {
                Message = message,
                ConversationId = conversationId,
                Stream = true
            }, cancellationToken))
            {
                await Response.WriteAsync($"data: {chunk.Replace("\n", "\\n")}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await Response.WriteAsync($"event: error\ndata: {ex.Message}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpDelete("conversation/{conversationId}")]
    public IActionResult ClearConversation(string conversationId)
    {
        projectManagementAgentService.ClearConversation(conversationId);
        zeroAssistantService.ClearConversationAsync(conversationId).GetAwaiter().GetResult();
        return NoContent();
    }

    [HttpGet("zero-history")]
    public async Task<IActionResult> ZeroHistory([FromQuery] string? conversationId, [FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        return Json(await zeroAssistantService.GetConversationHistoryAsync(conversationId, limit, cancellationToken));
    }

    [HttpPost("zero-chat")]
    public async Task<IActionResult> ZeroChat([FromBody] AiChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        try
        {
            return Json(await zeroAssistantService.SendTextAsync(request, cancellationToken));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpPost("voice")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Voice(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest(new { error = "Expected multipart form data." });
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var file = form.Files["audio"];
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "Missing audio file." });
        }

        var conversationId = form["conversationId"].ToString();
        await using var stream = file.OpenReadStream();
        try
        {
            return Json(await zeroAssistantService.ProcessVoiceAsync(stream, file.FileName, file.ContentType, conversationId, cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Zero voice request failed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Server voice input is unavailable. Use browser voice input or type the request." });
        }
    }

    [HttpPost("transcribe")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Transcribe(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest(new { error = "Expected multipart form data." });
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var file = form.Files["audio"];
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "Missing audio file." });
        }

        await using var stream = file.OpenReadStream();
        try
        {
            return Json(new { text = await zeroAssistantService.TranscribeVoiceAsync(stream, file.FileName, file.ContentType, cancellationToken) });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Zero transcription request failed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Server transcription is unavailable." });
        }
    }

    [HttpPost("zero-memory")]
    public async Task<IActionResult> AddZeroMemory([FromBody] ZeroMemoryRequest request, CancellationToken cancellationToken)
    {
        return Json(await zeroAssistantService.AddMemoryAsync(request.Text ?? string.Empty, cancellationToken));
    }

    [HttpPost("zero-memory/remove")]
    public async Task<IActionResult> RemoveZeroMemory([FromBody] ZeroMemoryRequest request, CancellationToken cancellationToken)
    {
        return Json(await zeroAssistantService.RemoveMemoryAsync(request.Text ?? string.Empty, cancellationToken));
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        return Json(await aiSettingsService.GetInputModelAsync(cancellationToken));
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        return Json(await aiProviderFactory.GetHealthAsync(cancellationToken));
    }

    [HttpPost("monitor")]
    public async Task<IActionResult> TriggerMonitoring([FromQuery] bool includeAiSummary = false, CancellationToken cancellationToken = default)
    {
        return Json(await projectMonitoringService.RunAsync(includeAiSummary, cancellationToken));
    }

    [HttpGet("actions")]
    public async Task<IActionResult> RecentActions(CancellationToken cancellationToken)
    {
        var actions = await aiAuditService.GetRecentAsync(50, cancellationToken);
        return Json(actions.Select(action => new
        {
            action.Id,
            action.ActionType,
            action.Summary,
            action.Outcome,
            action.Provider,
            action.ModelId,
            action.UserDisplayName,
            action.OccurredAtUtc
        }));
    }
}

public sealed record ZeroMemoryRequest(string? Text);
