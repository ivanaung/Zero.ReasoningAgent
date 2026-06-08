using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IOperationalUsageStore
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task EnsureReadyAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZeroConversationMessage>> GetZeroConversationHistoryAsync(string userId, string conversationId, int limit, CancellationToken cancellationToken = default);
    Task MirrorZeroConversationMessageAsync(ZeroConversationMessage message, CancellationToken cancellationToken = default);
    Task MirrorDeleteZeroConversationAsync(string userId, string conversationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiActionAudit>> GetRecentAiAuditsAsync(int take, CancellationToken cancellationToken = default);
    Task MirrorAiActionAuditAsync(AiActionAudit audit, CancellationToken cancellationToken = default);

    Task<List<FinanceTransaction>> GetFinanceTransactionsAsync(string userId, CancellationToken cancellationToken = default);
    Task<FinanceTransaction?> GetFinanceTransactionAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task MirrorFinanceTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default);
    Task MirrorDeleteFinanceTransactionAsync(int id, CancellationToken cancellationToken = default);
}

public class OperationalUsageStore(
    AppDbContext sqliteContext,
    IOperationalEventStore operationalEventStore) : IOperationalUsageStore
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        operationalEventStore.IsAvailableAsync(cancellationToken);

    public Task EnsureReadyAsync(CancellationToken cancellationToken = default) =>
        EnsureUsageReadyAsync(cancellationToken);

    private async Task EnsureUsageReadyAsync(CancellationToken cancellationToken)
    {
        await operationalEventStore.EnsureReadyAsync(cancellationToken);
        await using var context = await operationalEventStore.CreateOperationalContextAsync(cancellationToken);
        await SyncZeroConversationMessagesAsync(context, cancellationToken);
        await SyncAiActionAuditsAsync(context, cancellationToken);
        await SyncFinanceTransactionsAsync(context, cancellationToken);
    }

    public async Task<IReadOnlyList<ZeroConversationMessage>> GetZeroConversationHistoryAsync(string userId, string conversationId, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var items = await context.ZeroConversationMessages
            .Where(item => item.UserId == userId && item.ConversationId == conversationId)
            .OrderByDescending(item => item.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task MirrorZeroConversationMessageAsync(ZeroConversationMessage message, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var existing = await context.ZeroConversationMessages.FirstOrDefaultAsync(item => item.Id == message.Id, cancellationToken);
        if (existing == null)
        {
            context.ZeroConversationMessages.Add(Map(message));
        }
        else
        {
            Copy(message, existing);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MirrorDeleteZeroConversationAsync(string userId, string conversationId, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var rows = await context.ZeroConversationMessages
            .Where(item => item.UserId == userId && item.ConversationId == conversationId)
            .ToListAsync(cancellationToken);
        context.ZeroConversationMessages.RemoveRange(rows);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiActionAudit>> GetRecentAiAuditsAsync(int take, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var items = await context.AiActionAudits
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task MirrorAiActionAuditAsync(AiActionAudit audit, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var existing = await context.AiActionAudits.FirstOrDefaultAsync(item => item.Id == audit.Id, cancellationToken);
        if (existing == null)
        {
            context.AiActionAudits.Add(Map(audit));
        }
        else
        {
            Copy(audit, existing);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<FinanceTransaction>> GetFinanceTransactionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var items = await context.FinanceTransactions
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.TransactionDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);
        return await HydrateFinanceTransactionsAsync(items, cancellationToken);
    }

    public async Task<FinanceTransaction?> GetFinanceTransactionAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var item = await context.FinanceTransactions.FirstOrDefaultAsync(entry => entry.UserId == userId && entry.Id == id, cancellationToken);
        if (item == null)
        {
            return null;
        }

        return (await HydrateFinanceTransactionsAsync([item], cancellationToken)).FirstOrDefault();
    }

    public async Task MirrorFinanceTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var existing = await context.FinanceTransactions.FirstOrDefaultAsync(item => item.Id == transaction.Id, cancellationToken);
        if (existing == null)
        {
            context.FinanceTransactions.Add(Map(transaction));
        }
        else
        {
            Copy(transaction, existing);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MirrorDeleteFinanceTransactionAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var item = await context.FinanceTransactions.FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (item == null)
        {
            return;
        }

        context.FinanceTransactions.Remove(item);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<OperationalDbContext> CreateReadyContextAsync(CancellationToken cancellationToken)
    {
        return await operationalEventStore.CreateOperationalContextAsync(cancellationToken);
    }

    private async Task<List<FinanceTransaction>> HydrateFinanceTransactionsAsync(IReadOnlyList<OperationalFinanceTransaction> items, CancellationToken cancellationToken)
    {
        var mapped = items.Select(Map).ToList();
        if (mapped.Count == 0)
        {
            return mapped;
        }

        var accountIds = mapped.Select(item => item.FinanceAccountId).Distinct().ToList();
        var categoryIds = mapped.Select(item => item.FinanceCategoryId).Distinct().ToList();
        var projectIds = mapped.Where(item => item.ProjectId.HasValue).Select(item => item.ProjectId!.Value).Distinct().ToList();

        var accounts = await sqliteContext.FinanceAccounts.AsNoTracking().Where(item => accountIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var categories = await sqliteContext.FinanceCategories.AsNoTracking().Where(item => categoryIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var projects = projectIds.Count == 0
            ? new Dictionary<int, Project>()
            : await sqliteContext.Projects.AsNoTracking().Where(item => projectIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var item in mapped)
        {
            if (accounts.TryGetValue(item.FinanceAccountId, out var account))
            {
                item.FinanceAccount = account;
            }

            if (categories.TryGetValue(item.FinanceCategoryId, out var category))
            {
                item.FinanceCategory = category;
            }

            if (item.ProjectId.HasValue && projects.TryGetValue(item.ProjectId.Value, out var project))
            {
                item.Project = project;
            }
        }

        return mapped;
    }

    private async Task SyncZeroConversationMessagesAsync(OperationalDbContext context, CancellationToken cancellationToken)
    {
        var sqliteItems = await sqliteContext.ZeroConversationMessages.AsNoTracking().OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var existing = await context.ZeroConversationMessages.ToDictionaryAsync(item => item.Id, cancellationToken);
        var sqliteIds = sqliteItems.Select(item => item.Id).ToHashSet();

        foreach (var sqliteItem in sqliteItems)
        {
            if (existing.TryGetValue(sqliteItem.Id, out var target))
            {
                Copy(sqliteItem, target);
            }
            else
            {
                context.ZeroConversationMessages.Add(Map(sqliteItem));
            }
        }

        foreach (var orphan in existing.Values.Where(item => !sqliteIds.Contains(item.Id)))
        {
            context.ZeroConversationMessages.Remove(orphan);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncAiActionAuditsAsync(OperationalDbContext context, CancellationToken cancellationToken)
    {
        var sqliteItems = await sqliteContext.AiActionAudits.AsNoTracking().OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var existing = await context.AiActionAudits.ToDictionaryAsync(item => item.Id, cancellationToken);
        var sqliteIds = sqliteItems.Select(item => item.Id).ToHashSet();

        foreach (var sqliteItem in sqliteItems)
        {
            if (existing.TryGetValue(sqliteItem.Id, out var target))
            {
                Copy(sqliteItem, target);
            }
            else
            {
                context.AiActionAudits.Add(Map(sqliteItem));
            }
        }

        foreach (var orphan in existing.Values.Where(item => !sqliteIds.Contains(item.Id)))
        {
            context.AiActionAudits.Remove(orphan);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncFinanceTransactionsAsync(OperationalDbContext context, CancellationToken cancellationToken)
    {
        var sqliteItems = await sqliteContext.FinanceTransactions.AsNoTracking().OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var existing = await context.FinanceTransactions.ToDictionaryAsync(item => item.Id, cancellationToken);
        var sqliteIds = sqliteItems.Select(item => item.Id).ToHashSet();

        foreach (var sqliteItem in sqliteItems)
        {
            if (existing.TryGetValue(sqliteItem.Id, out var target))
            {
                Copy(sqliteItem, target);
            }
            else
            {
                context.FinanceTransactions.Add(Map(sqliteItem));
            }
        }

        foreach (var orphan in existing.Values.Where(item => !sqliteIds.Contains(item.Id)))
        {
            context.FinanceTransactions.Remove(orphan);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static ZeroConversationMessage Map(OperationalZeroConversationMessage item) => new()
    {
        Id = item.Id,
        UserId = item.UserId,
        ConversationId = item.ConversationId,
        Role = item.Role,
        Content = item.Content,
        CreatedUtc = item.CreatedUtc
    };

    private static OperationalZeroConversationMessage Map(ZeroConversationMessage item) => new()
    {
        Id = item.Id,
        UserId = item.UserId,
        ConversationId = item.ConversationId,
        Role = item.Role,
        Content = item.Content,
        CreatedUtc = item.CreatedUtc
    };

    private static void Copy(ZeroConversationMessage source, OperationalZeroConversationMessage target)
    {
        target.UserId = source.UserId;
        target.ConversationId = source.ConversationId;
        target.Role = source.Role;
        target.Content = source.Content;
        target.CreatedUtc = source.CreatedUtc;
    }

    private static AiActionAudit Map(OperationalAiActionAudit item) => new()
    {
        Id = item.Id,
        UserId = item.UserId,
        UserDisplayName = item.UserDisplayName,
        ActionType = item.ActionType,
        Provider = item.Provider,
        ModelId = item.ModelId,
        Summary = item.Summary,
        RequestPreview = item.RequestPreview,
        ResponsePreview = item.ResponsePreview,
        Outcome = item.Outcome,
        OccurredAtUtc = item.OccurredAtUtc
    };

    private static OperationalAiActionAudit Map(AiActionAudit item) => new()
    {
        Id = item.Id,
        UserId = item.UserId,
        UserDisplayName = item.UserDisplayName,
        ActionType = item.ActionType,
        Provider = item.Provider,
        ModelId = item.ModelId,
        Summary = item.Summary,
        RequestPreview = item.RequestPreview,
        ResponsePreview = item.ResponsePreview,
        Outcome = item.Outcome,
        OccurredAtUtc = item.OccurredAtUtc
    };

    private static void Copy(AiActionAudit source, OperationalAiActionAudit target)
    {
        target.UserId = source.UserId;
        target.UserDisplayName = source.UserDisplayName;
        target.ActionType = source.ActionType;
        target.Provider = source.Provider;
        target.ModelId = source.ModelId;
        target.Summary = source.Summary;
        target.RequestPreview = source.RequestPreview;
        target.ResponsePreview = source.ResponsePreview;
        target.Outcome = source.Outcome;
        target.OccurredAtUtc = source.OccurredAtUtc;
    }

    private static FinanceTransaction Map(OperationalFinanceTransaction item) => new()
    {
        Id = item.Id,
        UserId = item.UserId,
        FinanceAccountId = item.FinanceAccountId,
        FinanceCategoryId = item.FinanceCategoryId,
        ProjectId = item.ProjectId,
        Type = item.Type,
        Scope = item.Scope,
        Amount = item.Amount,
        TransactionDate = item.TransactionDate,
        Description = item.Description,
        PaymentMethod = item.PaymentMethod,
        Notes = item.Notes,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt
    };

    private static OperationalFinanceTransaction Map(FinanceTransaction item) => new()
    {
        Id = item.Id,
        UserId = item.UserId,
        FinanceAccountId = item.FinanceAccountId,
        FinanceCategoryId = item.FinanceCategoryId,
        ProjectId = item.ProjectId,
        Type = item.Type,
        Scope = item.Scope,
        Amount = item.Amount,
        TransactionDate = item.TransactionDate,
        Description = item.Description,
        PaymentMethod = item.PaymentMethod,
        Notes = item.Notes,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt
    };

    private static void Copy(FinanceTransaction source, OperationalFinanceTransaction target)
    {
        target.UserId = source.UserId;
        target.FinanceAccountId = source.FinanceAccountId;
        target.FinanceCategoryId = source.FinanceCategoryId;
        target.ProjectId = source.ProjectId;
        target.Type = source.Type;
        target.Scope = source.Scope;
        target.Amount = source.Amount;
        target.TransactionDate = source.TransactionDate;
        target.Description = source.Description;
        target.PaymentMethod = source.PaymentMethod;
        target.Notes = source.Notes;
        target.CreatedAt = source.CreatedAt;
        target.UpdatedAt = source.UpdatedAt;
    }
}
