using Microsoft.EntityFrameworkCore;
using Npgsql;
using ScheduleApp.Data;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IOperationalEventStore
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task EnsureReadyAsync(CancellationToken cancellationToken = default);
    Task<OperationalDbContext> CreateOperationalContextAsync(CancellationToken cancellationToken = default);
    Task<List<ScheduleEvent>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<ScheduleEvent>> GetEventsForDayAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<List<ScheduleEvent>> GetEventsInRangeAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<List<ScheduleEvent>> GetEventsByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<ScheduleEvent?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task MirrorUpsertAsync(ScheduleEvent evt, CancellationToken cancellationToken = default);
    Task MirrorDeleteAsync(int id, CancellationToken cancellationToken = default);
}

public class OperationalEventStore(
    AppDbContext sqliteContext,
    IOperationalDatabaseSettingsService operationalDatabaseSettingsService,
    ILogger<OperationalEventStore> logger) : IOperationalEventStore
{
    private static readonly SemaphoreSlim SyncLock = new(1, 1);
    private static string? readyConnectionString;
    private static DateTimeOffset readyCheckedAtUtc = DateTimeOffset.MinValue;
    private static bool ready;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = await operationalDatabaseSettingsService.GetConnectionStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        if (ready &&
            string.Equals(readyConnectionString, connectionString, StringComparison.Ordinal) &&
            DateTimeOffset.UtcNow - readyCheckedAtUtc < TimeSpan.FromMinutes(2))
        {
            return true;
        }

        try
        {
            await EnsureReadyAsync(cancellationToken);
            return ready && string.Equals(readyConnectionString, connectionString, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Operational PostgreSQL event store is unavailable. Falling back to SQLite Events.");
            return false;
        }
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = await operationalDatabaseSettingsService.GetConnectionStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ready = false;
            readyConnectionString = null;
            return;
        }

        if (ready &&
            string.Equals(readyConnectionString, connectionString, StringComparison.Ordinal) &&
            DateTimeOffset.UtcNow - readyCheckedAtUtc < TimeSpan.FromMinutes(2))
        {
            return;
        }

        await SyncLock.WaitAsync(cancellationToken);
        try
        {
            if (ready &&
                string.Equals(readyConnectionString, connectionString, StringComparison.Ordinal) &&
                DateTimeOffset.UtcNow - readyCheckedAtUtc < TimeSpan.FromMinutes(2))
            {
                return;
            }

            await using var operationalContext = CreateContext(connectionString);
            await operationalContext.Database.EnsureCreatedAsync(cancellationToken);
            await SyncFromSqliteAsync(operationalContext, cancellationToken);
            await operationalContext.Database.ExecuteSqlRawAsync("""
                SELECT setval(
                    pg_get_serial_sequence('"Events"', 'Id'),
                    COALESCE((SELECT MAX("Id") FROM "Events"), 1),
                    true
                );
                """, cancellationToken);

            ready = true;
            readyConnectionString = connectionString;
            readyCheckedAtUtc = DateTimeOffset.UtcNow;
        }
        catch
        {
            ready = false;
            readyConnectionString = null;
            readyCheckedAtUtc = DateTimeOffset.UtcNow;
            throw;
        }
        finally
        {
            SyncLock.Release();
        }
    }

    public async Task<List<ScheduleEvent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var events = await context.Events.OrderBy(e => e.StartDateTime).ToListAsync(cancellationToken);
        return await HydrateAsync(events, cancellationToken);
    }

    public async Task<List<ScheduleEvent>> GetEventsForDayAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);
        var events = await context.Events
            .Where(e => e.StartDateTime < dayEnd && e.EndDateTime >= dayStart)
            .OrderBy(e => e.IsAllDay ? 0 : 1)
            .ThenBy(e => e.StartDateTime)
            .ToListAsync(cancellationToken);
        return await HydrateAsync(events, cancellationToken);
    }

    public async Task<List<ScheduleEvent>> GetEventsInRangeAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var events = await context.Events
            .Where(e => e.StartDateTime < end && e.EndDateTime > start)
            .OrderBy(e => e.StartDateTime)
            .ToListAsync(cancellationToken);
        return await HydrateAsync(events, cancellationToken);
    }

    public async Task<List<ScheduleEvent>> GetEventsByProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var events = await context.Events
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.StartDateTime)
            .ToListAsync(cancellationToken);
        return await HydrateAsync(events, cancellationToken);
    }

    public async Task<ScheduleEvent?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var context = await CreateReadyContextAsync(cancellationToken);
        var evt = await context.Events.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (evt == null)
        {
            return null;
        }

        var hydrated = await HydrateAsync([evt], cancellationToken);
        return hydrated.FirstOrDefault();
    }

    public Task<OperationalDbContext> CreateOperationalContextAsync(CancellationToken cancellationToken = default) =>
        CreateReadyContextAsync(cancellationToken);

    public async Task MirrorUpsertAsync(ScheduleEvent evt, CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
        {
            return;
        }

        await using var context = await CreateReadyContextAsync(cancellationToken);
        var existing = await context.Events.FirstOrDefaultAsync(item => item.Id == evt.Id, cancellationToken);
        if (existing == null)
        {
            context.Events.Add(Map(evt));
        }
        else
        {
            Copy(evt, existing);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MirrorDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
        {
            return;
        }

        await using var context = await CreateReadyContextAsync(cancellationToken);
        var existing = await context.Events.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (existing == null)
        {
            return;
        }

        context.Events.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncFromSqliteAsync(OperationalDbContext operationalContext, CancellationToken cancellationToken)
    {
        var sqliteEvents = await sqliteContext.Events.AsNoTracking().OrderBy(e => e.Id).ToListAsync(cancellationToken);
        var operationalEvents = await operationalContext.Events.ToDictionaryAsync(item => item.Id, cancellationToken);
        var sqliteIds = sqliteEvents.Select(item => item.Id).ToHashSet();

        foreach (var sqliteEvent in sqliteEvents)
        {
            if (operationalEvents.TryGetValue(sqliteEvent.Id, out var existing))
            {
                Copy(sqliteEvent, existing);
            }
            else
            {
                operationalContext.Events.Add(Map(sqliteEvent));
            }
        }

        foreach (var orphan in operationalEvents.Values.Where(item => !sqliteIds.Contains(item.Id)))
        {
            operationalContext.Events.Remove(orphan);
        }

        await operationalContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<ScheduleEvent>> HydrateAsync(IReadOnlyList<OperationalScheduleEvent> operationalEvents, CancellationToken cancellationToken)
    {
        var items = operationalEvents.Select(Map).ToList();
        if (items.Count == 0)
        {
            return items;
        }

        var categoryIds = items.Where(item => item.CategoryId.HasValue).Select(item => item.CategoryId!.Value).Distinct().ToList();
        var projectIds = items.Where(item => item.ProjectId.HasValue).Select(item => item.ProjectId!.Value).Distinct().ToList();
        var stageIds = items.Where(item => item.StageId.HasValue).Select(item => item.StageId!.Value).Distinct().ToList();
        var dependsOnIds = items.Where(item => item.DependsOnId.HasValue).Select(item => item.DependsOnId!.Value).Distinct().ToList();

        var categories = categoryIds.Count == 0
            ? new Dictionary<int, Category>()
            : await sqliteContext.Categories.AsNoTracking().Where(item => categoryIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var projects = projectIds.Count == 0
            ? new Dictionary<int, Project>()
            : await sqliteContext.Projects.AsNoTracking().Where(item => projectIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var stages = stageIds.Count == 0
            ? new Dictionary<int, ProjectStage>()
            : await sqliteContext.ProjectStages.AsNoTracking().Where(item => stageIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var dependencies = dependsOnIds.Count == 0
            ? new Dictionary<int, ScheduleEvent>()
            : (await sqliteContext.Events.AsNoTracking().Where(item => dependsOnIds.Contains(item.Id)).ToListAsync(cancellationToken)).ToDictionary(item => item.Id);

        foreach (var item in items)
        {
            if (item.CategoryId.HasValue && categories.TryGetValue(item.CategoryId.Value, out var category))
            {
                item.Category = category;
            }

            if (item.ProjectId.HasValue && projects.TryGetValue(item.ProjectId.Value, out var project))
            {
                item.Project = project;
            }

            if (item.StageId.HasValue && stages.TryGetValue(item.StageId.Value, out var stage))
            {
                item.Stage = stage;
            }

            if (item.DependsOnId.HasValue && dependencies.TryGetValue(item.DependsOnId.Value, out var dependency))
            {
                item.DependsOn = dependency;
            }
        }

        return items;
    }

    private async Task<OperationalDbContext> CreateReadyContextAsync(CancellationToken cancellationToken)
    {
        var connectionString = await operationalDatabaseSettingsService.GetConnectionStringAsync(cancellationToken)
            ?? throw new InvalidOperationException("Operational PostgreSQL connection is not configured.");
        return CreateContext(connectionString);
    }

    private static OperationalDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<OperationalDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new OperationalDbContext(options);
    }

    private static OperationalScheduleEvent Map(ScheduleEvent evt) => new()
    {
        Id = evt.Id,
        Title = evt.Title,
        Description = evt.Description,
        StartDateTime = evt.StartDateTime,
        EndDateTime = evt.EndDateTime,
        IsAllDay = evt.IsAllDay,
        Status = evt.Status,
        Priority = evt.Priority,
        Progress = evt.Progress,
        Icon = evt.Icon,
        CategoryId = evt.CategoryId,
        ProjectId = evt.ProjectId,
        StageId = evt.StageId,
        DependsOnId = evt.DependsOnId,
        IsTodoListTask = evt.IsTodoListTask,
        AreaName = evt.AreaName,
        Color = evt.Color,
        AssignedTo = evt.AssignedTo,
        IsRecurring = evt.IsRecurring,
        RecurrenceRule = evt.RecurrenceRule,
        CreatedAt = evt.CreatedAt,
        UpdatedAt = evt.UpdatedAt
    };

    private static ScheduleEvent Map(OperationalScheduleEvent evt) => new()
    {
        Id = evt.Id,
        Title = evt.Title,
        Description = evt.Description,
        StartDateTime = evt.StartDateTime,
        EndDateTime = evt.EndDateTime,
        IsAllDay = evt.IsAllDay,
        Status = evt.Status,
        Priority = evt.Priority,
        Progress = evt.Progress,
        Icon = evt.Icon,
        CategoryId = evt.CategoryId,
        ProjectId = evt.ProjectId,
        StageId = evt.StageId,
        DependsOnId = evt.DependsOnId,
        IsTodoListTask = evt.IsTodoListTask,
        AreaName = evt.AreaName,
        Color = evt.Color,
        AssignedTo = evt.AssignedTo,
        IsRecurring = evt.IsRecurring,
        RecurrenceRule = evt.RecurrenceRule,
        CreatedAt = evt.CreatedAt,
        UpdatedAt = evt.UpdatedAt
    };

    private static void Copy(ScheduleEvent source, OperationalScheduleEvent target)
    {
        target.Title = source.Title;
        target.Description = source.Description;
        target.StartDateTime = source.StartDateTime;
        target.EndDateTime = source.EndDateTime;
        target.IsAllDay = source.IsAllDay;
        target.Status = source.Status;
        target.Priority = source.Priority;
        target.Progress = source.Progress;
        target.Icon = source.Icon;
        target.CategoryId = source.CategoryId;
        target.ProjectId = source.ProjectId;
        target.StageId = source.StageId;
        target.DependsOnId = source.DependsOnId;
        target.IsTodoListTask = source.IsTodoListTask;
        target.AreaName = source.AreaName;
        target.Color = source.Color;
        target.AssignedTo = source.AssignedTo;
        target.IsRecurring = source.IsRecurring;
        target.RecurrenceRule = source.RecurrenceRule;
        target.CreatedAt = source.CreatedAt;
        target.UpdatedAt = source.UpdatedAt;
    }
}
