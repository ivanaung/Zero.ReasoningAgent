using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(int? projectId = null, CancellationToken cancellationToken = default);
}

public class DashboardService(
    IProjectService projectService,
    IEventService eventService,
    ICurrentUserService currentUserService,
    IUserProactivePreferenceService preferenceService,
    IRecommendationQueryService recommendationQueryService,
    IRecommendationComposer recommendationComposer,
    INotificationCenterService notificationCenterService,
    IProjectStageService projectStageService,
    IFinanceService financeService) : IDashboardService
{
    public async Task<DashboardViewModel> GetDashboardAsync(int? projectId = null, CancellationToken cancellationToken = default)
    {
        var projects = await projectService.GetAllAsync();
        var globalStages = await projectStageService.GetAllAsync();
        var events = await eventService.GetAllAsync();
        var today = DateTime.Today;
        var selectedProject = projectId.HasValue
            ? projects.FirstOrDefault(project => project.Id == projectId.Value)
            : null;

        selectedProject ??= projects.FirstOrDefault(project => project.Status == ProjectStatus.Active) ?? projects.FirstOrDefault();
        var scopedEvents = selectedProject != null
            ? events.Where(evt => evt.ProjectId == selectedProject.Id).ToList()
            : events;

        var dashboard = new DashboardViewModel
        {
            ActiveProjects = projects.Count(p => p.Status == ProjectStatus.Active),
            OpenTasks = events.Count(e => e.Status is EventStatus.Todo or EventStatus.InProgress or EventStatus.Blocked),
            CompletedToday = events.Count(e => e.Status == EventStatus.Done && e.UpdatedAt.Date == today),
            OverdueTasks = events.Count(e => e.IsOverdue),
            SelectedProjectId = selectedProject?.Id,
            SelectedProjectName = selectedProject?.Name ?? "All Projects",
            Projects = projects.Select(project => new ProjectOptionItemViewModel
            {
                Id = project.Id,
                Name = project.Name
            }).ToList(),
            ProjectProgress = projects
                .Select(project =>
                {
                    var total = project.Events.Count;
                    var done = project.Events.Count(e => e.Status == EventStatus.Done);
                    var progress = total > 0 ? (int)Math.Round((double)done / total * 100) : 0;

                    return new ProjectProgressItemViewModel
                    {
                        Name = project.Name,
                        Progress = progress,
                        Color = string.IsNullOrWhiteSpace(project.Color) ? "#2E5DA6" : project.Color!
                    };
                })
                .OrderByDescending(item => item.Progress)
                .ThenBy(item => item.Name)
                .ToList(),
            StageProgress = globalStages
                .OrderBy(stage => stage.Name)
                .Select(stage =>
                {
                    var stageEvents = scopedEvents.Where(evt => evt.StageId == stage.Id).ToList();
                    var totalTasks = stageEvents.Count;
                    var completedTasks = stageEvents.Count(evt => evt.Status == EventStatus.Done);
                    var progress = totalTasks > 0 ? (int)Math.Round(stageEvents.Average(evt => evt.Progress)) : 0;

                    return new StageProgressItemViewModel
                    {
                        Id = stage.Id,
                        Name = stage.Name,
                        Progress = progress,
                        TotalTasks = totalTasks,
                        CompletedTasks = completedTasks,
                        Color = selectedProject != null && !string.IsNullOrWhiteSpace(selectedProject.Color) 
                            ? selectedProject.Color! 
                            : "#2E5DA6"
                    };
                })
                .ToList(),
            StatusBreakdown = Enum.GetValues<EventStatus>()
                .Select(status => new StatusCountItemViewModel
                {
                    Label = status.ToString(),
                    Count = scopedEvents.Count(e => e.Status == status)
                })
                .ToList(),
            PriorityBreakdown = Enum.GetValues<EventPriority>()
                .Select(priority => new PriorityCountItemViewModel
                {
                    Label = priority.ToString(),
                    Count = scopedEvents.Count(e => e.Priority == priority && e.Status != EventStatus.Done)
                })
                .ToList(),
            UpcomingTasks = scopedEvents
                .Where(e => e.EndDateTime >= DateTime.Now && e.Status != EventStatus.Done)
                .OrderBy(e => e.StartDateTime)
                .Take(8)
                .Select(e => new AgendaItemViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    TimeLabel = e.IsAllDay
                        ? $"{e.StartDateTime:ddd dd MMM} · All day"
                        : $"{e.StartDateTime:ddd dd MMM} · {e.StartDateTime:HH:mm} - {e.EndDateTime:HH:mm}",
                    IsCritical = e.Priority == EventPriority.Critical,
                    Icon = e.Icon
                })
                .ToList(),
            ToDoTasks = events
                .Where(e => e.IsTodoListTask && (e.Status != EventStatus.Done || (e.Status == EventStatus.Done && e.UpdatedAt >= DateTime.Today.AddDays(-1))))
                .OrderBy(e => e.Status == EventStatus.Done ? 1 : 0)
                .ThenBy(e => e.StartDateTime)
                .Select(e => new AgendaItemViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    TimeLabel = e.IsAllDay
                        ? $"{e.StartDateTime:ddd dd MMM} · All day"
                        : $"{e.StartDateTime:ddd dd MMM} · {e.StartDateTime:HH:mm} - {e.EndDateTime:HH:mm}",
                    Status = e.Status.ToString(),
                    Color = !string.IsNullOrWhiteSpace(e.Category?.Color) ? e.Category.Color! : (!string.IsNullOrWhiteSpace(e.Color) ? e.Color! : "#2E5DA6"),
                    IsCritical = e.Priority == EventPriority.Critical,
                    IsDone = e.Status == EventStatus.Done,
                    Icon = e.Icon
                })
                .ToList()
        };

        var userId = currentUserService.UserId;
        if (!string.IsNullOrWhiteSpace(userId) && !string.Equals(userId, "anonymous", StringComparison.OrdinalIgnoreCase))
        {
            dashboard.FinanceSummary = await financeService.GetSummaryWidgetAsync(userId, cancellationToken);
            var preference = await preferenceService.GetInputAsync(userId, currentUserService.DisplayName, cancellationToken);
            var nextHourContext = await recommendationQueryService.BuildContextAsync(new RecommendationRequest
            {
                UserId = userId,
                UserDisplayName = currentUserService.DisplayName,
                TimeZone = preference.TimeZone,
                Horizon = RecommendationHorizon.NextHour,
                ReferenceTime = DateTimeOffset.UtcNow
            }, cancellationToken);
            dashboard.NextHourFocus = await recommendationComposer.ComposeAsync(nextHourContext, useAi: false, cancellationToken);

            var tomorrowContext = await recommendationQueryService.BuildContextAsync(new RecommendationRequest
            {
                UserId = userId,
                UserDisplayName = currentUserService.DisplayName,
                TimeZone = preference.TimeZone,
                Horizon = RecommendationHorizon.Tomorrow,
                ReferenceTime = DateTimeOffset.UtcNow
            }, cancellationToken);
            dashboard.TomorrowPreview = await recommendationComposer.ComposeAsync(tomorrowContext, useAi: false, cancellationToken);
            dashboard.UpcomingReminders = await notificationCenterService.GetItemsAsync(userId, cancellationToken);
        }

        return dashboard;
    }
}
