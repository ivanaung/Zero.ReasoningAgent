using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IZeroAssistantDataService
{
    Task<ZeroAssistantSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<ZeroAssistantSettingsInputViewModel> GetSettingsInputAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(ZeroAssistantSettingsInputViewModel model, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetMemoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> AddMemoryAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> RemoveMemoryAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ZeroConversationMessage>> GetHistoryAsync(string conversationId, int limit, CancellationToken cancellationToken = default);
    Task AddConversationTurnAsync(string conversationId, string role, string content, CancellationToken cancellationToken = default);
    Task ClearConversationAsync(string conversationId, CancellationToken cancellationToken = default);
}

public class ZeroAssistantDataService(
    AppDbContext context,
    ICurrentUserService currentUserService,
    IOperationalUsageStore operationalUsageStore,
    ILogger<ZeroAssistantDataService> logger) : IZeroAssistantDataService
{
    public async Task<ZeroAssistantSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await context.ZeroAssistantSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings != null)
        {
            return settings;
        }

        settings = new ZeroAssistantSettings();
        context.ZeroAssistantSettings.Add(settings);
        await context.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<ZeroAssistantSettingsInputViewModel> GetSettingsInputAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return new ZeroAssistantSettingsInputViewModel
        {
            EnableVoice = settings.EnableVoice,
            EnableLocalFileTools = settings.EnableLocalFileTools,
            EnableVisionTools = settings.EnableVisionTools,
            SearchProvider = settings.SearchProvider,
            WhisperUrl = settings.WhisperUrl,
            PiperUrl = settings.PiperUrl,
            PiperEndpoint = settings.PiperEndpoint,
            PiperVoice = settings.PiperVoice,
            SearXngBaseUrl = settings.SearXngBaseUrl,
            HistoryLimit = settings.HistoryLimit,
            RequestTimeoutSeconds = settings.RequestTimeoutSeconds,
            BrowserSpeechRate = settings.BrowserSpeechRate,
            BrowserSpeechPitch = settings.BrowserSpeechPitch,
            MemoryLimit = settings.MemoryLimit,
            MaxUploadMegabytes = settings.MaxUploadMegabytes
        };
    }

    public async Task SaveSettingsAsync(ZeroAssistantSettingsInputViewModel model, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        settings.EnableVoice = model.EnableVoice;
        settings.EnableLocalFileTools = model.EnableLocalFileTools;
        settings.EnableVisionTools = model.EnableVisionTools;
        settings.SearchProvider = model.SearchProvider;
        settings.WhisperUrl = model.WhisperUrl.Trim();
        settings.PiperUrl = model.PiperUrl.Trim();
        settings.PiperEndpoint = string.IsNullOrWhiteSpace(model.PiperEndpoint) ? "/" : model.PiperEndpoint.Trim();
        settings.PiperVoice = string.IsNullOrWhiteSpace(model.PiperVoice) ? string.Empty : model.PiperVoice.Trim();
        settings.SearXngBaseUrl = model.SearXngBaseUrl.Trim();
        settings.HistoryLimit = Math.Clamp(model.HistoryLimit, 1, 100);
        settings.RequestTimeoutSeconds = Math.Clamp(model.RequestTimeoutSeconds, 5, 300);
        settings.BrowserSpeechRate = Math.Clamp(model.BrowserSpeechRate, 0.5d, 2.0d);
        settings.BrowserSpeechPitch = Math.Clamp(model.BrowserSpeechPitch, 0.5d, 2.0d);
        settings.MemoryLimit = Math.Clamp(model.MemoryLimit, 1, 30);
        settings.MaxUploadMegabytes = Math.Clamp(model.MaxUploadMegabytes, 1, 25);
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetMemoryAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        return await context.ZeroMemoryItems
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.SortOrder)
            .Select(item => item.Text)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> AddMemoryAsync(string text, CancellationToken cancellationToken = default)
    {
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return await GetMemoryAsync(cancellationToken);
        }

        var settings = await GetSettingsAsync(cancellationToken);
        var memory = (await GetMemoryAsync(cancellationToken)).ToList();
        memory.RemoveAll(item => string.Equals(item, text, StringComparison.OrdinalIgnoreCase));
        memory.Insert(0, text);
        if (memory.Count > settings.MemoryLimit)
        {
            memory.RemoveRange(settings.MemoryLimit, memory.Count - settings.MemoryLimit);
        }

        await SaveMemoryListAsync(memory, cancellationToken);
        return memory;
    }

    public async Task<IReadOnlyList<string>> RemoveMemoryAsync(string text, CancellationToken cancellationToken = default)
    {
        var memory = (await GetMemoryAsync(cancellationToken)).ToList();
        memory.RemoveAll(item => string.Equals(item, text, StringComparison.OrdinalIgnoreCase));
        await SaveMemoryListAsync(memory, cancellationToken);
        return memory;
    }

    public async Task<IReadOnlyList<ZeroConversationMessage>> GetHistoryAsync(string conversationId, int limit, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        if (await UseOperationalStoreAsync(cancellationToken))
        {
            try
            {
                return await operationalUsageStore.GetZeroConversationHistoryAsync(userId, conversationId, limit, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read Zero conversation history from PostgreSQL. Falling back to SQLite.");
            }
        }

        return await context.ZeroConversationMessages
            .Where(item => item.UserId == userId && item.ConversationId == conversationId)
            .OrderByDescending(item => item.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddConversationTurnAsync(string conversationId, string role, string content, CancellationToken cancellationToken = default)
    {
        var message = new ZeroConversationMessage
        {
            UserId = GetRequiredUserId(),
            ConversationId = conversationId,
            Role = role,
            Content = content.Length > 8000 ? content[..8000] : content,
            CreatedUtc = DateTime.UtcNow
        };
        context.ZeroConversationMessages.Add(message);

        await context.SaveChangesAsync(cancellationToken);
        if (await UseOperationalStoreAsync(cancellationToken))
        {
            try
            {
                await operationalUsageStore.MirrorZeroConversationMessageAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to mirror Zero conversation message {MessageId} to PostgreSQL.", message.Id);
            }
        }
    }

    public async Task ClearConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var rows = await context.ZeroConversationMessages
            .Where(item => item.UserId == userId && item.ConversationId == conversationId)
            .ToListAsync(cancellationToken);
        context.ZeroConversationMessages.RemoveRange(rows);
        await context.SaveChangesAsync(cancellationToken);
        if (await UseOperationalStoreAsync(cancellationToken))
        {
            try
            {
                await operationalUsageStore.MirrorDeleteZeroConversationAsync(userId, conversationId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete Zero conversation {ConversationId} from PostgreSQL mirror.", conversationId);
            }
        }
    }

    private async Task SaveMemoryListAsync(IReadOnlyList<string> memory, CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        var existing = await context.ZeroMemoryItems
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);
        context.ZeroMemoryItems.RemoveRange(existing);

        for (var index = 0; index < memory.Count; index++)
        {
            context.ZeroMemoryItems.Add(new ZeroMemoryItem
            {
                UserId = userId,
                Text = memory[index],
                SortOrder = index,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private string GetRequiredUserId()
    {
        return currentUserService.IsAuthenticated
            ? currentUserService.UserId
            : "anonymous";
    }

    private async Task<bool> UseOperationalStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await operationalUsageStore.IsAvailableAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Operational usage store check failed for Zero history. Falling back to SQLite.");
            return false;
        }
    }
}
