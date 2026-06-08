using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IProjectMonitoringService
{
    Task<ProjectMonitoringResultViewModel> RunAsync(bool includeAiSummary = false, CancellationToken cancellationToken = default);
}

public class ProjectMonitoringService(
    AppDbContext context,
    IAiSettingsService aiSettingsService,
    IEventService eventService,
    IAiAuditService auditService,
    IProjectManagementAgentService projectManagementAgentService) : IProjectMonitoringService
{
    public async Task<ProjectMonitoringResultViewModel> RunAsync(bool includeAiSummary = false, CancellationToken cancellationToken = default)
    {
        var settings = await aiSettingsService.GetAsync(cancellationToken);
        var now = DateTime.Now;
        var projects = await context.Projects.Include(project => project.Events).ToListAsync(cancellationToken);
        var overdueTasks = projects.SelectMany(project => project.Events).Where(evt => evt.Status != EventStatus.Done && evt.EndDateTime < now).ToList();
        var blockedTasks = projects.SelectMany(project => project.Events).Where(evt => evt.Status == EventStatus.Blocked).ToList();
        var milestonesAtRisk = projects.SelectMany(project => project.Events)
            .Count(evt => evt.Priority == EventPriority.Critical && evt.Status != EventStatus.Done && evt.EndDateTime <= now.AddDays(3));

        var result = new ProjectMonitoringResultViewModel
        {
            ProjectCount = projects.Count,
            OverdueTaskCount = overdueTasks.Count,
            BlockedTaskCount = blockedTasks.Count,
            MilestonesAtRisk = milestonesAtRisk,
            Summary = $"Projects inspected={projects.Count}; overdueTasks={overdueTasks.Count}; blockedTasks={blockedTasks.Count}; milestonesAtRisk={milestonesAtRisk}."
        };

        if (settings.EnableProgressMonitoring && settings.AutoCreateLowRiskTasks)
        {
            foreach (var task in overdueTasks.Take(3))
            {
                var existingReminder = await context.Events.AnyAsync(evt =>
                    evt.Title == $"Follow up: {task.Title}"
                    && evt.ProjectId == task.ProjectId
                    && evt.CreatedAt >= now.Date,
                    cancellationToken);

                if (existingReminder)
                {
                    continue;
                }

                await eventService.CreateAsync(new ScheduleEvent
                {
                    Title = $"Follow up: {task.Title}",
                    Description = $"Auto-created reminder for overdue task {task.Id}.",
                    ProjectId = task.ProjectId,
                    StageId = task.StageId,
                    AreaName = task.AreaName,
                    StartDateTime = now.AddHours(1),
                    EndDateTime = now.AddHours(2),
                    Priority = EventPriority.Low,
                    Status = EventStatus.Todo,
                    AssignedTo = task.AssignedTo
                });

                result.AutoCreatedTasks++;
            }
        }

        if (includeAiSummary && settings.IsEnabled)
        {
            var chat = await projectManagementAgentService.SendAsync(new AiChatRequest
            {
                Message = $"Summarize project risk from this monitoring snapshot in 4 concise sentences: {result.Summary}"
            }, cancellationToken);

            result.AgentSummary = chat.Message;
        }

        await auditService.LogAsync("monitoring.run", result.Summary, "Succeeded", result.Summary, result.AgentSummary, settings.ProviderType.ToString(), settings.ModelId, cancellationToken);
        return result;
    }
}
