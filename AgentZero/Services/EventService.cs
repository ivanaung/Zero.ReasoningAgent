using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IEventService
{
    Task<List<ScheduleEvent>> GetAllAsync();
    Task<List<ScheduleEvent>> GetEventsForDayAsync(DateTime date);
    Task<List<ScheduleEvent>> GetEventsInRangeAsync(DateTime start, DateTime end);
    Task<List<ScheduleEvent>> GetEventsByProjectAsync(int projectId);
    Task<ScheduleEvent?> GetByIdAsync(int id);
    Task CreateAsync(ScheduleEvent evt);
    Task UpdateAsync(ScheduleEvent evt);
    Task DeleteAsync(int id);
    Task UpdateStatusAsync(int id, EventStatus status);
    Task RunAutomationsAsync(); // New: Health check and status automation
}

public class EventService(
    AppDbContext context,
    INotificationPlannerService notificationPlannerService,
    IOperationalEventStore operationalEventStore,
    ILogger<EventService> logger) : IEventService
{
    public async Task<List<ScheduleEvent>> GetAllAsync()
    {
        if (await UseOperationalStoreAsync())
        {
            try
            {
                return await operationalEventStore.GetAllAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read Events from PostgreSQL. Falling back to SQLite.");
            }
        }

        return await GetAllFromSqliteAsync();
    }

    public async Task<List<ScheduleEvent>> GetEventsForDayAsync(DateTime date)
    {
        if (await UseOperationalStoreAsync())
        {
            try
            {
                return await operationalEventStore.GetEventsForDayAsync(date);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read day Events from PostgreSQL. Falling back to SQLite.");
            }
        }

        return await GetEventsForDayFromSqliteAsync(date);
    }

    public async Task<List<ScheduleEvent>> GetEventsInRangeAsync(DateTime start, DateTime end)
    {
        if (await UseOperationalStoreAsync())
        {
            try
            {
                return await operationalEventStore.GetEventsInRangeAsync(start, end);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read range Events from PostgreSQL. Falling back to SQLite.");
            }
        }

        return await GetEventsInRangeFromSqliteAsync(start, end);
    }

    public async Task<List<ScheduleEvent>> GetEventsByProjectAsync(int projectId)
    {
        if (await UseOperationalStoreAsync())
        {
            try
            {
                return await operationalEventStore.GetEventsByProjectAsync(projectId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read project Events from PostgreSQL. Falling back to SQLite.");
            }
        }

        return await GetEventsByProjectFromSqliteAsync(projectId);
    }

    public async Task<ScheduleEvent?> GetByIdAsync(int id)
    {
        if (await UseOperationalStoreAsync())
        {
            try
            {
                return await operationalEventStore.GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read Event {EventId} from PostgreSQL. Falling back to SQLite.", id);
            }
        }

        return await GetByIdFromSqliteAsync(id);
    }

    public async Task CreateAsync(ScheduleEvent evt)
    {
        context.Events.Add(evt);
        await context.SaveChangesAsync();
        await MirrorUpsertAsync(evt);
        await notificationPlannerService.RecomputeNotificationPlanForTaskAsync(evt.Id, "TaskCreated");
        await RunAutomationsAsync();
    }

    public async Task UpdateAsync(ScheduleEvent evt)
    {
        evt.UpdatedAt = DateTime.Now;
        context.Entry(evt).State = EntityState.Modified;
        await context.SaveChangesAsync();
        await MirrorUpsertAsync(evt);
        await notificationPlannerService.RecomputeNotificationPlanForTaskAsync(evt.Id, "TaskUpdated");
        await RunAutomationsAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var evt = await context.Events.FindAsync(id);
        if (evt != null)
        {
            await notificationPlannerService.CancelNotificationScheduleEntriesForTaskAsync(evt.Id, "TaskDeleted");
            context.Events.Remove(evt);
            await context.SaveChangesAsync();
            await MirrorDeleteAsync(evt.Id);
            await RunAutomationsAsync();
        }
    }

    public async Task UpdateStatusAsync(int id, EventStatus status)
    {
        var evt = await context.Events.FindAsync(id);
        if (evt != null)
        {
            evt.Status = status;
            evt.UpdatedAt = DateTime.Now;
            await context.SaveChangesAsync();
            await MirrorUpsertAsync(evt);
            await notificationPlannerService.RecomputeNotificationPlanForTaskAsync(evt.Id, "TaskStatusChanged");
            await RunAutomationsAsync();
        }
    }

    public async Task RunAutomationsAsync()
    {
        var allEvents = await context.Events.Include(e => e.DependsOn).ToListAsync();
        var changed = false;
        var changedIds = new HashSet<int>();

        foreach (var evt in allEvents)
        {
            if (evt.DependsOnId.HasValue && evt.DependsOn != null)
            {
                if (evt.DependsOn.Status != EventStatus.Done && evt.Status != EventStatus.Blocked && evt.Status != EventStatus.Done)
                {
                    evt.Status = EventStatus.Blocked;
                    changed = true;
                    changedIds.Add(evt.Id);
                }
                else if (evt.DependsOn.Status == EventStatus.Done && evt.Status == EventStatus.Blocked)
                {
                    evt.Status = EventStatus.Todo;
                    changed = true;
                    changedIds.Add(evt.Id);
                }
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync();
            foreach (var changedEvent in allEvents.Where(item => changedIds.Contains(item.Id)))
            {
                await MirrorUpsertAsync(changedEvent);
            }

            foreach (var eventId in changedIds)
            {
                await notificationPlannerService.RecomputeNotificationPlanForTaskAsync(eventId, "DependencyChanged");
            }
        }
    }

    private async Task<bool> UseOperationalStoreAsync()
    {
        try
        {
            return await operationalEventStore.IsAvailableAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Operational PostgreSQL event store check failed. Falling back to SQLite.");
            return false;
        }
    }

    private async Task MirrorUpsertAsync(ScheduleEvent evt)
    {
        try
        {
            await operationalEventStore.MirrorUpsertAsync(evt);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mirror event {EventId} to PostgreSQL. SQLite remains authoritative fallback.", evt.Id);
        }
    }

    private async Task MirrorDeleteAsync(int eventId)
    {
        try
        {
            await operationalEventStore.MirrorDeleteAsync(eventId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mirror event delete {EventId} to PostgreSQL. SQLite remains authoritative fallback.", eventId);
        }
    }

    private Task<List<ScheduleEvent>> GetAllFromSqliteAsync()
    {
        return context.Events
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.Stage)
            .Include(e => e.DependsOn)
            .OrderBy(e => e.StartDateTime)
            .ToListAsync();
    }

    private Task<List<ScheduleEvent>> GetEventsForDayFromSqliteAsync(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);
        return context.Events
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.Stage)
            .Include(e => e.DependsOn)
            .Where(e => e.StartDateTime < dayEnd && e.EndDateTime >= dayStart)
            .OrderBy(e => e.IsAllDay ? 0 : 1)
            .ThenBy(e => e.StartDateTime)
            .ToListAsync();
    }

    private Task<List<ScheduleEvent>> GetEventsInRangeFromSqliteAsync(DateTime start, DateTime end)
    {
        return context.Events
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.Stage)
            .Include(e => e.DependsOn)
            .Where(e => e.StartDateTime < end && e.EndDateTime > start)
            .OrderBy(e => e.StartDateTime)
            .ToListAsync();
    }

    private Task<List<ScheduleEvent>> GetEventsByProjectFromSqliteAsync(int projectId)
    {
        return context.Events
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.Stage)
            .Include(e => e.DependsOn)
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.StartDateTime)
            .ToListAsync();
    }

    private Task<ScheduleEvent?> GetByIdFromSqliteAsync(int id)
    {
        return context.Events
            .Include(e => e.Category)
            .Include(e => e.Project)
            .Include(e => e.Stage)
            .Include(e => e.DependsOn)
            .FirstOrDefaultAsync(e => e.Id == id);
    }
}
