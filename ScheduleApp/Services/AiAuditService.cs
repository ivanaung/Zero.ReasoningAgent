using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IAiAuditService
{
    Task LogAsync(
        string actionType,
        string summary,
        string outcome,
        string? requestPreview = null,
        string? responsePreview = null,
        string? provider = null,
        string? modelId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiActionAudit>> GetRecentAsync(int take = 50, CancellationToken cancellationToken = default);
}

public class AiAuditService(
    AppDbContext context,
    ICurrentUserService currentUserService,
    IOperationalUsageStore operationalUsageStore,
    ILogger<AiAuditService> logger) : IAiAuditService
{
    public async Task LogAsync(
        string actionType,
        string summary,
        string outcome,
        string? requestPreview = null,
        string? responsePreview = null,
        string? provider = null,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        var audit = new AiActionAudit
        {
            ActionType = actionType,
            Summary = summary,
            Outcome = outcome,
            RequestPreview = Redact(requestPreview),
            ResponsePreview = Redact(responsePreview),
            Provider = provider,
            ModelId = modelId,
            UserId = currentUserService.UserId,
            UserDisplayName = currentUserService.DisplayName
        };
        context.AiActionAudits.Add(audit);

        await context.SaveChangesAsync(cancellationToken);
        if (await UseOperationalStoreAsync(cancellationToken))
        {
            try
            {
                await operationalUsageStore.MirrorAiActionAuditAsync(audit, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to mirror AI audit {AuditId} to PostgreSQL.", audit.Id);
            }
        }
    }

    public async Task<IReadOnlyList<AiActionAudit>> GetRecentAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        if (await UseOperationalStoreAsync(cancellationToken))
        {
            try
            {
                return await operationalUsageStore.GetRecentAiAuditsAsync(take, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read AI audit history from PostgreSQL. Falling back to SQLite.");
            }
        }

        return await context.AiActionAudits
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private static string? Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var result = value.Replace("Bearer ", "Bearer [redacted] ", StringComparison.OrdinalIgnoreCase);
        return result.Length > 3500 ? result[..3500] : result;
    }

    private async Task<bool> UseOperationalStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await operationalUsageStore.IsAvailableAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Operational usage store check failed for AI audit history. Falling back to SQLite.");
            return false;
        }
    }
}
