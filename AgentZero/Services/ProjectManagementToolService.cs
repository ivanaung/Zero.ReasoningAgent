using System.ComponentModel;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IProjectManagementToolService
{
    Task<string> GetProjectSummary(int projectId, CancellationToken cancellationToken = default);
    Task<string> GetTaskSummary(int taskId, CancellationToken cancellationToken = default);
    Task<string> GetTasksByProject(int projectId, CancellationToken cancellationToken = default);
    Task<string> GetOverdueTasks(int? projectId = null, CancellationToken cancellationToken = default);
    Task<string> GetBlockedTasks(int? projectId = null, CancellationToken cancellationToken = default);
    Task<string> GetUpcomingMilestones(int? projectId = null, CancellationToken cancellationToken = default);
    Task<string> SearchProjects(string query, CancellationToken cancellationToken = default);
    Task<string> SearchTasks(string query, CancellationToken cancellationToken = default);
    Task<string> FindTaskByName(string name, CancellationToken cancellationToken = default);
    Task<TaskLookupResult?> FindBestTaskMatchAsync(string query, CancellationToken cancellationToken = default);
    
    [Description("Creates a new top-level Project. Use this when the user explicitly wants to create a Project or Product, not just a task.")]
    Task<string> CreateProject(
        [Description("The exact name of the project or product to create.")] string name, 
        [Description("Optional details about the project.")] string? description = null,
        [Description("Optional color code in hex format (e.g. #FF5733).")] string? color = null,
        CancellationToken cancellationToken = default);

    Task<string> CreateTask(string title, string? description = null, int? projectId = null, string? projectName = null, int? dependsOnTaskId = null, int? categoryId = null, int? stageId = null, string? stageName = null, string? assignee = null, string? startHint = null, string? endHint = null, bool? allDay = null, CancellationToken cancellationToken = default);
    Task<string> UpdateTaskDates(int taskId, string? startDateTime = null, string? endDateTime = null, string? reason = null, CancellationToken cancellationToken = default);
    Task<string> AssignTask(int taskId, string assignee, CancellationToken cancellationToken = default);
    Task<string> AddTaskComment(int taskId, string comment, CancellationToken cancellationToken = default);
    Task<string> AddProjectComment(int projectId, string comment, CancellationToken cancellationToken = default);
    Task<string> GetTeamAvailability(string? userId = null, string? from = null, string? to = null, CancellationToken cancellationToken = default);
    Task<string> GetUserAssignedTasks(string userId, string? from = null, string? to = null, CancellationToken cancellationToken = default);
    Task<string> GetDueSoonTasks(string userId, string? from = null, string? to = null, CancellationToken cancellationToken = default);
    Task<string> GetProjectRisks(string? userId = null, int? projectId = null, CancellationToken cancellationToken = default);
    Task<string> GetCalendarContext(string userId, string? from = null, string? to = null, CancellationToken cancellationToken = default);
    [Description("Returns the status of the linked Google email and calendar integration, including whether an account is connected.")]
    Task<string> GetGoogleIntegrationStatus(CancellationToken cancellationToken = default);
    [Description("Returns a recent summary of linked Gmail inbox messages. Use this for questions about new email or inbox activity.")]
    Task<string> GetInboxSummary(int maxItems = 10, CancellationToken cancellationToken = default);
    [Description("Searches linked Gmail inbox messages using a keyword query. Use this for questions about whether an email exists or who sent it.")]
    Task<string> SearchInboxEmails(string query, int maxItems = 5, CancellationToken cancellationToken = default);
    [Description("Returns upcoming events from the linked Google Calendar account in a requested time window.")]
    Task<string> GetLinkedCalendarEvents(string? from = null, string? to = null, int maxItems = 10, CancellationToken cancellationToken = default);
    Task<string> CalculateNextWorkingSlot(string? after = null, int durationMinutes = 60, string? preferredDate = null, string? preferredTime = null, CancellationToken cancellationToken = default);
    Task<string> GetDependencyChain(int taskId, CancellationToken cancellationToken = default);
    string GetCurrentDateTime();
    Task<string> GetWorkingCalendarRules(CancellationToken cancellationToken = default);
    Task<string> CreateFollowUpTask(int taskId, string title, string? assignee = null, CancellationToken cancellationToken = default);
    Task<string> CreateNotificationScheduleEntry(string userId, string notificationType, string scheduledForUtc, string? message = null, int? taskId = null, int? projectId = null, CancellationToken cancellationToken = default);
    Task<string> CancelNotificationScheduleEntriesForTask(int taskId, CancellationToken cancellationToken = default);
    Task<string> RecomputeNotificationPlanForTask(int taskId, CancellationToken cancellationToken = default);
    Task<string> GetNotificationContext(int notificationId, CancellationToken cancellationToken = default);
    string ConvertTime(string targetTimeZone, string? timeToConvert = null);
    Task<string> GetFinanceSummary(string? scope = null, string? month = null, CancellationToken cancellationToken = default);
    Task<string> GetUpcomingBills(int days = 7, CancellationToken cancellationToken = default);
    Task<string> GetProjectFinanceSummary(int projectId, CancellationToken cancellationToken = default);
    Task<string> CreateFinanceTransaction(string title, decimal amount, string scope = "Business", string type = "Expense", string? categoryName = null, int? projectId = null, string? transactionDate = null, CancellationToken cancellationToken = default);
}

public sealed record TaskLookupResult(
    int TaskId,
    string Title,
    string? ProjectName,
    string Status,
    DateTime StartDateTime,
    DateTime EndDateTime,
    bool IsOverdue);

public class ProjectManagementToolService(
    AppDbContext context,
    IEventService eventService,
    IProjectService projectService,
    IProjectStageService projectStageService,
    IWorkingCalendarService workingCalendarService,
    IAiAuditService auditService,
    ICurrentUserService currentUserService,
    IAiSettingsService aiSettingsService,
    INotificationPlannerService notificationPlannerService,
    IGoogleIntegrationService googleIntegrationService,
    IFinanceService financeService) : IProjectManagementToolService
{
    public async Task<string> GetProjectSummary(int projectId, CancellationToken cancellationToken = default)
    {
        var project = await projectService.GetByIdAsync(projectId);
        if (project == null)
        {
            return $"Project {projectId} was not found.";
        }

        var overdue = project.Events.Count(evt => evt.IsOverdue);
        var blocked = project.Events.Count(evt => evt.Status == EventStatus.Blocked);
        var done = project.Events.Count(evt => evt.Status == EventStatus.Done);
        var summary = $"Project {project.Name}: status={project.Status}, totalTasks={project.Events.Count}, done={done}, blocked={blocked}, overdue={overdue}.";
        await auditService.LogAsync("tool.project-summary", summary, "Succeeded", summary, null, cancellationToken: cancellationToken);
        return summary;
    }

    public async Task<string> GetTaskSummary(int taskId, CancellationToken cancellationToken = default)
    {
        var task = await eventService.GetByIdAsync(taskId);
        if (task == null)
        {
            return $"Task {taskId} was not found.";
        }

        var summary = $"Task {task.Id}: {task.Title}; status={task.Status}; project={task.Project?.Name ?? "None"}; stage={task.Stage?.Name ?? task.AreaName ?? "None"}; start={task.StartDateTime:yyyy-MM-dd HH:mm}; end={task.EndDateTime:yyyy-MM-dd HH:mm}; assignedTo={task.AssignedTo ?? "Unassigned"}; dependency={(task.DependsOn?.Title ?? "None")}.";
        await auditService.LogAsync("tool.task-summary", summary, "Succeeded", summary, null, cancellationToken: cancellationToken);
        return summary;
    }

    public async Task<string> GetTasksByProject(int projectId, CancellationToken cancellationToken = default)
    {
        var tasks = await eventService.GetEventsByProjectAsync(projectId);
        return FormatTaskList(tasks, $"Tasks for project {projectId}");
    }

    public async Task<string> GetOverdueTasks(int? projectId = null, CancellationToken cancellationToken = default)
    {
        var query = context.Events
            .Include(evt => evt.Project)
            .Where(evt => evt.Status != EventStatus.Done && evt.EndDateTime < DateTime.Now);

        if (projectId.HasValue)
        {
            query = query.Where(evt => evt.ProjectId == projectId.Value);
        }

        return FormatTaskList(await query.OrderBy(evt => evt.EndDateTime).ToListAsync(cancellationToken), "Overdue tasks");
    }

    public async Task<string> GetBlockedTasks(int? projectId = null, CancellationToken cancellationToken = default)
    {
        var query = context.Events
            .Include(evt => evt.Project)
            .Where(evt => evt.Status == EventStatus.Blocked);

        if (projectId.HasValue)
        {
            query = query.Where(evt => evt.ProjectId == projectId.Value);
        }

        return FormatTaskList(await query.OrderBy(evt => evt.StartDateTime).ToListAsync(cancellationToken), "Blocked tasks");
    }

    public async Task<string> GetUpcomingMilestones(int? projectId = null, CancellationToken cancellationToken = default)
    {
        var query = context.Events
            .Include(evt => evt.Project)
            .Where(evt => evt.Priority == EventPriority.Critical && evt.EndDateTime >= DateTime.Now);

        if (projectId.HasValue)
        {
            query = query.Where(evt => evt.ProjectId == projectId.Value);
        }

        return FormatTaskList(await query.OrderBy(evt => evt.EndDateTime).Take(10).ToListAsync(cancellationToken), "Upcoming milestones");
    }

    public async Task<string> SearchProjects(string query, CancellationToken cancellationToken = default)
    {
        var projects = await context.Projects
            .Where(project => project.Name.Contains(query) || (project.Description != null && project.Description.Contains(query)))
            .OrderBy(project => project.Name)
            .Take(10)
            .ToListAsync(cancellationToken);

        return projects.Count == 0
            ? $"No projects matched '{query}'."
            : "Matching projects: " + string.Join(" | ", projects.Select(project => $"{project.Id}:{project.Name} ({project.Status})"));
    }

    public async Task<string> SearchTasks(string query, CancellationToken cancellationToken = default)
    {
        var tasks = await FindTaskCandidatesAsync(query, cancellationToken);
        var ranked = RankTasks(tasks, query)
            .Take(12)
            .ToList();

        return FormatTaskList(ranked, $"Matching tasks for '{query}'");
    }

    [Description("Finds the best matching task by task name or short title only. Use this for questions like 'when did Rejo expire' or 'find task Rejo'. Pass only the likely task name, not the whole sentence.")]
    public async Task<string> FindTaskByName(
        [Description("The task name or short title to match, for example 'Rejo'.")] string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Task name is required.";
        }

        var match = await FindBestTaskMatchAsync(name, cancellationToken);
        if (match == null)
        {
            return $"No task matched '{name}'.";
        }

        return $"Best task match: {match.TaskId}:{match.Title}; status={match.Status}; start={match.StartDateTime:yyyy-MM-dd HH:mm}; end={match.EndDateTime:yyyy-MM-dd HH:mm}; overdue={(match.IsOverdue ? "yes" : "no")}; project={match.ProjectName ?? "None"}.";
    }

    public async Task<TaskLookupResult?> FindBestTaskMatchAsync(string query, CancellationToken cancellationToken = default)
    {
        var tasks = await FindTaskCandidatesAsync(query, cancellationToken);
        var bestMatch = RankTasks(tasks, query).FirstOrDefault();
        if (bestMatch == null)
        {
            return null;
        }

        return new TaskLookupResult(
            bestMatch.Id,
            bestMatch.Title,
            bestMatch.Project?.Name,
            bestMatch.Status.ToString(),
            bestMatch.StartDateTime,
            bestMatch.EndDateTime,
            bestMatch.IsOverdue);
    }

    [Description("Creates a new top-level Project. Use this when the user explicitly wants to create a Project or Product, not just a task.")]
    public async Task<string> CreateProject(
        [Description("The exact name of the project or product to create.")] string name, 
        [Description("Optional details about the project.")] string? description = null,
        [Description("Optional color code in hex format (e.g. #FF5733).")] string? color = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Project name is required.";
        }

        var project = new Project
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            Color = string.IsNullOrWhiteSpace(color) ? "#2E5DA6" : color.Trim(),
            Status = ProjectStatus.Active,
            CreatedAt = DateTime.Now
        };

        await projectService.CreateAsync(project);
        await auditService.LogAsync("tool.create-project", $"Created project {project.Name} ({project.Id}).", "Succeeded", name, $"ProjectId={project.Id}", cancellationToken: cancellationToken);
        return $"Created project {project.Id}: {project.Name} successfully.";
    }

    public async Task<string> CreateTask(string title, string? description = null, int? projectId = null, string? projectName = null, int? dependsOnTaskId = null, int? categoryId = null, int? stageId = null, string? stageName = null, string? assignee = null, string? startHint = null, string? endHint = null, bool? allDay = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Task title is required.";
        }

        var resolvedProjectId = projectId;
        if (!resolvedProjectId.HasValue && !string.IsNullOrWhiteSpace(projectName))
        {
            resolvedProjectId = await context.Projects
                .Where(project => project.Name == projectName)
                .Select(project => (int?)project.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        int? resolvedStageId = stageId;
        string? areaName = null;
        if (!resolvedStageId.HasValue && resolvedProjectId.HasValue && !string.IsNullOrWhiteSpace(stageName))
        {
            var stage = await projectStageService.EnsureStageAsync(stageName);
            resolvedStageId = stage.Id;
            areaName = stage.Name;
        }
        else if (resolvedStageId.HasValue)
        {
            var stage = await projectStageService.GetByIdAsync(resolvedStageId.Value);
            areaName = stage?.Name;
        }

        var dependency = dependsOnTaskId.HasValue
            ? await eventService.GetByIdAsync(dependsOnTaskId.Value)
            : null;

        var suggestedStart = ParseDateTimeOffset(startHint);
        if (dependency != null && (!suggestedStart.HasValue || suggestedStart.Value.UtcDateTime < dependency.EndDateTime))
        {
            suggestedStart = new DateTimeOffset(DateTime.SpecifyKind(dependency.EndDateTime, DateTimeKind.Local));
        }

        var duration = TimeSpan.FromHours(1);
        var parsedEnd = ParseDateTimeOffset(endHint);
        if (suggestedStart.HasValue && parsedEnd.HasValue && parsedEnd > suggestedStart)
        {
            duration = parsedEnd.Value - suggestedStart.Value;
        }

        var schedule = await workingCalendarService.ScheduleAsync(suggestedStart, duration, cancellationToken);

        var resolvedAssignee = !string.IsNullOrWhiteSpace(assignee)
            ? assignee.Trim()
            : (currentUserService.IsAuthenticated ? currentUserService.UserId : null);

        var task = new ScheduleEvent
        {
            Title = title.Trim(),
            Description = description?.Trim(),
            ProjectId = resolvedProjectId,
            StageId = resolvedStageId,
            AreaName = areaName,
            CategoryId = categoryId,
            DependsOnId = dependsOnTaskId,
            AssignedTo = resolvedAssignee,
            IsAllDay = allDay ?? false,
            StartDateTime = schedule.Start.LocalDateTime,
            EndDateTime = schedule.End.LocalDateTime
        };

        await eventService.CreateAsync(task);
        await auditService.LogAsync("tool.create-task", $"Created task {task.Title} ({task.Id}).", "Succeeded", title, $"TaskId={task.Id}", cancellationToken: cancellationToken);
        return $"Created task {task.Id}:{task.Title} scheduled {task.StartDateTime:yyyy-MM-dd HH:mm} to {task.EndDateTime:yyyy-MM-dd HH:mm}.";
    }

    public async Task<string> UpdateTaskDates(int taskId, string? startDateTime = null, string? endDateTime = null, string? reason = null, CancellationToken cancellationToken = default)
    {
        var settings = await aiSettingsService.GetAsync(cancellationToken);
        if (settings.RequireApprovalForScheduleChange)
        {
            return "Approval required: schedule changes are configured to require approval before changing dates.";
        }

        var task = await eventService.GetByIdAsync(taskId);
        if (task == null)
        {
            return $"Task {taskId} was not found.";
        }

        var currentDuration = task.EndDateTime - task.StartDateTime;
        var preferredStart = ParseDateTimeOffset(startDateTime)
            ?? new DateTimeOffset(DateTime.SpecifyKind(task.StartDateTime, DateTimeKind.Local));
        var preferredEnd = ParseDateTimeOffset(endDateTime);
        var duration = preferredEnd.HasValue && preferredEnd > preferredStart
            ? preferredEnd.Value - preferredStart
            : currentDuration;

        var schedule = await workingCalendarService.ScheduleAsync(preferredStart, duration, cancellationToken);
        task.StartDateTime = schedule.Start.LocalDateTime;
        task.EndDateTime = schedule.End.LocalDateTime;
        await eventService.UpdateAsync(task);
        await auditService.LogAsync("tool.update-task-dates", $"Updated dates for task {task.Id}.", "Succeeded", reason, $"{task.StartDateTime:o}|{task.EndDateTime:o}", cancellationToken: cancellationToken);
        return $"Updated task {task.Id}:{task.Title} to {task.StartDateTime:yyyy-MM-dd HH:mm} - {task.EndDateTime:yyyy-MM-dd HH:mm}.";
    }

    public async Task<string> AssignTask(int taskId, string assignee, CancellationToken cancellationToken = default)
    {
        var task = await eventService.GetByIdAsync(taskId);
        if (task == null)
        {
            return $"Task {taskId} was not found.";
        }

        task.AssignedTo = assignee.Trim();
        await eventService.UpdateAsync(task);
        await auditService.LogAsync("tool.assign-task", $"Assigned task {task.Id} to {task.AssignedTo}.", "Succeeded", assignee, null, cancellationToken: cancellationToken);
        return $"Assigned task {task.Id}:{task.Title} to {task.AssignedTo}.";
    }

    public async Task<string> AddTaskComment(int taskId, string comment, CancellationToken cancellationToken = default)
    {
        var task = await eventService.GetByIdAsync(taskId);
        if (task == null)
        {
            return $"Task {taskId} was not found.";
        }

        context.TaskComments.Add(new TaskComment
        {
            TaskId = taskId,
            AuthorId = currentUserService.UserId,
            AuthorName = currentUserService.DisplayName,
            Content = comment.Trim()
        });
        await context.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync("tool.add-task-comment", $"Added comment to task {task.Id}.", "Succeeded", comment, null, cancellationToken: cancellationToken);
        return $"Added comment to task {task.Id}:{task.Title}.";
    }

    public async Task<string> AddProjectComment(int projectId, string comment, CancellationToken cancellationToken = default)
    {
        var project = await projectService.GetByIdAsync(projectId);
        if (project == null)
        {
            return $"Project {projectId} was not found.";
        }

        context.ProjectComments.Add(new ProjectComment
        {
            ProjectId = projectId,
            AuthorId = currentUserService.UserId,
            AuthorName = currentUserService.DisplayName,
            Content = comment.Trim()
        });
        await context.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync("tool.add-project-comment", $"Added comment to project {project.Id}.", "Succeeded", comment, null, cancellationToken: cancellationToken);
        return $"Added comment to project {project.Id}:{project.Name}.";
    }

    public async Task<string> GetTeamAvailability(string? userId = null, string? from = null, string? to = null, CancellationToken cancellationToken = default)
    {
        var start = ParseDateTimeOffset(from) ?? DateTimeOffset.UtcNow;
        var end = ParseDateTimeOffset(to) ?? start.AddDays(5);
        var tasks = await context.Events
            .Where(evt => string.IsNullOrEmpty(userId) || evt.AssignedTo == userId)
            .Where(evt => evt.EndDateTime >= start.UtcDateTime && evt.StartDateTime <= end.UtcDateTime)
            .OrderBy(evt => evt.StartDateTime)
            .ToListAsync(cancellationToken);

        var label = string.IsNullOrWhiteSpace(userId) ? "team" : userId;
        return tasks.Count == 0
            ? $"No scheduled assignments were found for {label} between {start:yyyy-MM-dd} and {end:yyyy-MM-dd}."
            : $"Availability for {label}: " + string.Join(" | ", tasks.Select(evt => $"{evt.AssignedTo ?? "unassigned"}:{evt.Title} {evt.StartDateTime:yyyy-MM-dd HH:mm}-{evt.EndDateTime:HH:mm}"));
    }

    public async Task<string> GetUserAssignedTasks(string userId, string? from = null, string? to = null, CancellationToken cancellationToken = default)
    {
        var start = ParseDateTimeOffset(from)?.LocalDateTime ?? DateTime.Now.AddDays(-1);
        var end = ParseDateTimeOffset(to)?.LocalDateTime ?? DateTime.Now.AddDays(7);
        var tasks = await context.Events
            .Include(evt => evt.Project)
            .Where(evt => evt.AssignedTo == userId && evt.EndDateTime >= start && evt.StartDateTime <= end)
            .OrderBy(evt => evt.StartDateTime)
            .ToListAsync(cancellationToken);
        return FormatTaskList(tasks, $"Assignments for {userId}");
    }

    public async Task<string> GetDueSoonTasks(string userId, string? from = null, string? to = null, CancellationToken cancellationToken = default)
    {
        var start = ParseDateTimeOffset(from)?.LocalDateTime ?? DateTime.Now;
        var end = ParseDateTimeOffset(to)?.LocalDateTime ?? DateTime.Now.AddHours(24);
        var tasks = await context.Events
            .Include(evt => evt.Project)
            .Where(evt => evt.AssignedTo == userId && evt.Status != EventStatus.Done && evt.EndDateTime >= start && evt.EndDateTime <= end)
            .OrderBy(evt => evt.EndDateTime)
            .ToListAsync(cancellationToken);
        return FormatTaskList(tasks, $"Due soon for {userId}");
    }

    public async Task<string> GetProjectRisks(string? userId = null, int? projectId = null, CancellationToken cancellationToken = default)
    {
        var query = context.Events.Include(evt => evt.Project).Where(evt => evt.Status == EventStatus.Blocked || (evt.Status != EventStatus.Done && evt.EndDateTime < DateTime.Now));
        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(evt => evt.AssignedTo == userId);
        }
        if (projectId.HasValue)
        {
            query = query.Where(evt => evt.ProjectId == projectId.Value);
        }

        return FormatTaskList(await query.OrderBy(evt => evt.EndDateTime).Take(12).ToListAsync(cancellationToken), "Project risks");
    }

    public async Task<string> GetCalendarContext(string userId, string? from = null, string? to = null, CancellationToken cancellationToken = default)
    {
        return await GetUserAssignedTasks(userId, from, to, cancellationToken);
    }

    public async Task<string> GetGoogleIntegrationStatus(CancellationToken cancellationToken = default)
    {
        var status = await googleIntegrationService.GetStatusAsync(cancellationToken);
        return $"Google integration: enabled={status.IsEnabled}; configured={status.IsConfigured}; connected={status.IsConnected}; emailEnabled={status.EmailEnabled}; calendarEnabled={status.CalendarEnabled}; account={status.ConnectedEmail ?? "None"}; status={status.StatusMessage}";
    }

    public async Task<string> GetInboxSummary(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        var messages = await googleIntegrationService.GetInboxMessagesAsync(maxItems, cancellationToken: cancellationToken);
        if (messages.Count == 0)
        {
            return "Inbox summary: no messages available from the linked Google account.";
        }

        return "Inbox summary: " + string.Join(" | ", messages.Select(item =>
            $"{item.Subject} from {item.From} at {(item.ReceivedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "unknown time")}"));
    }

    public async Task<string> SearchInboxEmails(string query, int maxItems = 5, CancellationToken cancellationToken = default)
    {
        var messages = await googleIntegrationService.GetInboxMessagesAsync(maxItems, query, cancellationToken);
        if (messages.Count == 0)
        {
            return $"Inbox search for '{query}': none.";
        }

        return $"Inbox search for '{query}': " + string.Join(" | ", messages.Select(item =>
            $"{item.Subject} from {item.From} at {(item.ReceivedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "unknown time")}"));
    }

    public async Task<string> GetLinkedCalendarEvents(string? from = null, string? to = null, int maxItems = 10, CancellationToken cancellationToken = default)
    {
        var events = await googleIntegrationService.GetUpcomingCalendarEventsAsync(ParseDateTimeOffset(from), ParseDateTimeOffset(to), maxItems, cancellationToken);
        if (events.Count == 0)
        {
            return "Linked calendar events: none.";
        }

        return "Linked calendar events: " + string.Join(" | ", events.Select(item =>
            $"{item.Title} {(item.Start?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "unknown")} - {(item.End?.ToLocalTime().ToString("HH:mm") ?? "unknown")} organizer={item.Organizer ?? "Unknown"}"));
    }

    public async Task<string> CalculateNextWorkingSlot(string? after = null, int durationMinutes = 60, string? preferredDate = null, string? preferredTime = null, CancellationToken cancellationToken = default)
    {
        var baseTime = ParseDateTimeOffset(after)
            ?? ComposePreferredDateTime(preferredDate, preferredTime)
            ?? DateTimeOffset.UtcNow;

        var slot = await workingCalendarService.CalculateNextWorkingSlotAsync(baseTime, TimeSpan.FromMinutes(Math.Max(15, durationMinutes)), cancellationToken);
        return $"Next working slot starts at {slot.LocalDateTime:yyyy-MM-dd HH:mm}.";
    }

    public async Task<string> GetDependencyChain(int taskId, CancellationToken cancellationToken = default)
    {
        var visited = new List<string>();
        var current = await eventService.GetByIdAsync(taskId);
        while (current != null)
        {
            visited.Add($"{current.Id}:{current.Title}");
            current = current.DependsOnId.HasValue
                ? await eventService.GetByIdAsync(current.DependsOnId.Value)
                : null;
        }

        return visited.Count == 0 ? $"Task {taskId} was not found." : "Dependency chain: " + string.Join(" -> ", visited);
    }

    public string GetCurrentDateTime()
    {
        return $"Current server time is {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}.";
    }

    public async Task<string> GetWorkingCalendarRules(CancellationToken cancellationToken = default)
    {
        var rules = await workingCalendarService.GetRulesAsync(cancellationToken);
        return $"Working calendar: timezone={rules.TimeZoneId}; start={rules.Start:hh\\:mm}; end={rules.End:hh\\:mm}; days={string.Join(",", rules.WorkingDays)}.";
    }

    public async Task<string> CreateFollowUpTask(int taskId, string title, string? assignee = null, CancellationToken cancellationToken = default)
    {
        var source = await eventService.GetByIdAsync(taskId);
        if (source == null)
        {
            return $"Task {taskId} was not found.";
        }

        return await CreateTask(title, $"Follow-up for task {source.Id}:{source.Title}.", source.ProjectId, source.Project?.Name, taskId, source.CategoryId, source.StageId, source.Stage?.Name ?? source.AreaName, assignee ?? source.AssignedTo, source.EndDateTime.ToString("o"), null, false, cancellationToken);
    }

    public async Task<string> CreateNotificationScheduleEntry(string userId, string notificationType, string scheduledForUtc, string? message = null, int? taskId = null, int? projectId = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<NotificationType>(notificationType, true, out var parsedType))
        {
            return $"Unsupported notification type '{notificationType}'.";
        }

        if (!DateTime.TryParse(scheduledForUtc, out var parsedDate))
        {
            return "Scheduled time is invalid.";
        }

        var entry = new NotificationScheduleEntry
        {
            UserId = userId,
            UserDisplayName = userId,
            ProjectId = projectId,
            TaskId = taskId,
            NotificationType = parsedType,
            ScheduledForUtc = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc),
            TimeZone = "UTC",
            TriggerSource = "Manual",
            DeduplicationKey = $"manual:{userId}:{parsedType}:{projectId}:{taskId}:{parsedDate:yyyyMMddHHmm}",
            Message = message ?? $"{parsedType} notification."
        };

        await notificationPlannerService.CreateNotificationScheduleEntryAsync(entry, cancellationToken);
        return $"Scheduled notification {entry.DeduplicationKey}.";
    }

    public async Task<string> CancelNotificationScheduleEntriesForTask(int taskId, CancellationToken cancellationToken = default)
    {
        await notificationPlannerService.CancelNotificationScheduleEntriesForTaskAsync(taskId, "Manual", cancellationToken);
        return $"Cancelled pending notification entries for task {taskId}.";
    }

    public async Task<string> RecomputeNotificationPlanForTask(int taskId, CancellationToken cancellationToken = default)
    {
        await notificationPlannerService.RecomputeNotificationPlanForTaskAsync(taskId, "Manual", cancellationToken);
        return $"Recomputed notification plan for task {taskId}.";
    }

    public async Task<string> GetNotificationContext(int notificationId, CancellationToken cancellationToken = default)
    {
        var entry = await context.NotificationScheduleEntries.FirstOrDefaultAsync(item => item.Id == notificationId, cancellationToken);
        if (entry == null)
        {
            return $"Notification {notificationId} was not found.";
        }

        return $"Notification {entry.Id}: type={entry.NotificationType}; status={entry.Status}; scheduledFor={entry.ScheduledForUtc:o}; message={entry.Message}";
    }

    [Description("Converts the time to a specific city, country, or time zone abbreviation. You MUST use this tool to get the precise time. When responding to the user, ONLY provide the clock time. Do not write descriptions.")]
    public string ConvertTime(
        [Description("The target location or time zone (e.g., 'Singapore', 'London', 'Tokyo', 'Pacific/Auckland').")] string targetTimeZone, 
        [Description("The time to convert. Leave empty to use current real-time.")] string? timeToConvert = null)
    {
        var sourceDate = string.IsNullOrWhiteSpace(timeToConvert) 
            ? DateTimeOffset.Now 
            : ParseDateTimeOffset(timeToConvert) ?? DateTimeOffset.Now;

        try
        {
            var tzInfo = ResolveTimeZone(targetTimeZone);
            var converted = TimeZoneInfo.ConvertTime(sourceDate, tzInfo);
            return $"Converted to {tzInfo.Id}: {converted:h:mm tt} ({converted:yyyy-MM-dd}). SYSTEM INSTRUCTION: You MUST reply to the user with EXACTLY THIS TIME and nothing else. Do not explain the timezone or provide background information.";
        }
        catch (Exception ex)
        {
            return $"Error converting time matching '{targetTimeZone}': {ex.Message}. Hint: try using explicit city or country names.";
        }
    }

    public async Task<string> GetFinanceSummary(string? scope = null, string? month = null, CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId;
        var parsedScope = Enum.TryParse<FinanceScope>(scope, true, out var scopeValue) ? scopeValue : (FinanceScope?)null;
        DateTime? monthStart = null;
        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse($"{month}-01", out var parsedMonth))
        {
            monthStart = parsedMonth;
        }

        return await financeService.GetFinanceSummaryAsync(userId, monthStart, parsedScope, cancellationToken);
    }

    public async Task<string> GetUpcomingBills(int days = 7, CancellationToken cancellationToken = default)
    {
        return await financeService.GetUpcomingBillsAsync(currentUserService.UserId, days, cancellationToken);
    }

    public async Task<string> GetProjectFinanceSummary(int projectId, CancellationToken cancellationToken = default)
    {
        return await financeService.GetProjectFinanceSummaryAsync(currentUserService.UserId, projectId, cancellationToken);
    }

    public async Task<string> CreateFinanceTransaction(string title, decimal amount, string scope = "Business", string type = "Expense", string? categoryName = null, int? projectId = null, string? transactionDate = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<FinanceScope>(scope, true, out var parsedScope))
        {
            parsedScope = FinanceScope.Business;
        }

        if (!Enum.TryParse<FinanceTransactionType>(type, true, out var parsedType))
        {
            parsedType = FinanceTransactionType.Expense;
        }

        DateTime? parsedDate = null;
        if (!string.IsNullOrWhiteSpace(transactionDate) && DateTime.TryParse(transactionDate, out var dt))
        {
            parsedDate = dt;
        }

        return await financeService.CreateFinanceTransactionAsync(currentUserService.UserId, title, amount, parsedScope, parsedType, categoryName, projectId, parsedDate, cancellationToken);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneString)
    {
        var normalized = timeZoneString.Trim().ToLowerInvariant();
        if (normalized == "utc" || normalized == "gmt") return TimeZoneInfo.Utc;
        if (normalized == "local") return TimeZoneInfo.Local;
        if (normalized == "nzt" || normalized == "nzst" || normalized == "nzdt") return TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "New Zealand Standard Time" : "Pacific/Auckland");
        if (normalized == "sgt" || normalized == "sgd") return TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Singapore");
        if (normalized == "pst" || normalized == "pdt") return TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Pacific Standard Time" : "America/Los_Angeles");
        if (normalized == "est" || normalized == "edt") return TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");
        if (normalized == "cst" || normalized == "cdt") return TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Central Standard Time" : "America/Chicago");
        
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneString);
        }
        catch (TimeZoneNotFoundException)
        {
            var all = TimeZoneInfo.GetSystemTimeZones();
            var match = all.FirstOrDefault(t => 
                t.Id.Contains(timeZoneString, StringComparison.OrdinalIgnoreCase) || 
                t.DisplayName.Contains(timeZoneString, StringComparison.OrdinalIgnoreCase) ||
                t.StandardName.Contains(timeZoneString, StringComparison.OrdinalIgnoreCase));
            
            if (match != null) return match;
            throw;
        }
    }

    private static string FormatTaskList(IEnumerable<ScheduleEvent> tasks, string title)
    {
        var items = tasks.Take(12).Select(evt => $"{evt.Id}:{evt.Title} [{evt.Status}] due {evt.EndDateTime:yyyy-MM-dd HH:mm} project={evt.Project?.Name ?? "None"}");
        var content = items.ToList();
        return content.Count == 0 ? $"{title}: none." : $"{title}: {string.Join(" | ", content)}";
    }

    private async Task<List<ScheduleEvent>> FindTaskCandidatesAsync(string query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var tokens = ExtractTaskSearchTokens(normalizedQuery);
        var eventQuery = context.Events.Include(evt => evt.Project).AsQueryable();

        if (tokens.Count == 0)
        {
            eventQuery = eventQuery.Where(evt =>
                evt.Title.Contains(normalizedQuery) ||
                (evt.Description != null && evt.Description.Contains(normalizedQuery)) ||
                (evt.Project != null && evt.Project.Name.Contains(normalizedQuery)));
        }
        else
        {
            eventQuery = eventQuery.Where(evt =>
                tokens.Any(token =>
                    evt.Title.Contains(token) ||
                    (evt.Description != null && evt.Description.Contains(token)) ||
                    (evt.Project != null && evt.Project.Name.Contains(token))));
        }

        return await eventQuery
            .OrderByDescending(evt => evt.UpdatedAt)
            .ThenBy(evt => evt.StartDateTime)
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    private static IEnumerable<ScheduleEvent> RankTasks(IEnumerable<ScheduleEvent> tasks, string query)
    {
        var normalizedQuery = query.Trim().ToLowerInvariant();
        var compactQuery = string.Concat(ExtractTaskSearchTokens(query));
        var tokens = ExtractTaskSearchTokens(query);

        return tasks
            .Select(evt => new
            {
                Task = evt,
                Score = ScoreTaskMatch(evt, normalizedQuery, compactQuery, tokens)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Task.EndDateTime)
            .Select(item => item.Task);
    }

    private static int ScoreTaskMatch(ScheduleEvent task, string normalizedQuery, string compactQuery, IReadOnlyList<string> tokens)
    {
        var title = task.Title.ToLowerInvariant();
        var description = task.Description?.ToLowerInvariant() ?? string.Empty;
        var project = task.Project?.Name.ToLowerInvariant() ?? string.Empty;
        var compactTitle = title.Replace(" ", string.Empty, StringComparison.Ordinal);
        var score = 0;

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            if (title == normalizedQuery)
            {
                score += 150;
            }

            if (title.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                score += 80;
            }

            if (description.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                score += 30;
            }

            if (project.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                score += 25;
            }
        }

        if (!string.IsNullOrWhiteSpace(compactQuery) && compactTitle.Contains(compactQuery, StringComparison.Ordinal))
        {
            score += 35;
        }

        foreach (var token in tokens)
        {
            if (title.Contains(token, StringComparison.Ordinal))
            {
                score += 20;
            }

            if (project.Contains(token, StringComparison.Ordinal))
            {
                score += 10;
            }

            if (description.Contains(token, StringComparison.Ordinal))
            {
                score += 6;
            }
        }

        return score;
    }

    private static IReadOnlyList<string> ExtractTaskSearchTokens(string query)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "are", "at", "blocked", "complete", "completed", "did", "do", "does",
            "due", "end", "ended", "expire", "expired", "expiry", "find", "for", "from", "in",
            "is", "me", "our", "overdue", "please", "project", "show", "status", "task", "tell",
            "the", "their", "this", "to", "was", "what", "when", "which", "with"
        };

        return System.Text.RegularExpressions.Regex.Matches(query.ToLowerInvariant(), "[a-z0-9][a-z0-9\\-]*")
            .Select(match => match.Value)
            .Where(token => token.Length >= 2 && !stopWords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value
            .Replace("SGD time", "+08:00", StringComparison.OrdinalIgnoreCase)
            .Replace("SGT", "+08:00", StringComparison.OrdinalIgnoreCase)
            .Replace("Singapore time", "+08:00", StringComparison.OrdinalIgnoreCase)
            .Replace("NZT", "+12:00", StringComparison.OrdinalIgnoreCase)
            .Replace("NZDT", "+13:00", StringComparison.OrdinalIgnoreCase);

        return DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? ComposePreferredDateTime(string? preferredDate, string? preferredTime)
    {
        if (string.IsNullOrWhiteSpace(preferredDate))
        {
            return null;
        }

        var composite = string.IsNullOrWhiteSpace(preferredTime)
            ? preferredDate
            : $"{preferredDate} {preferredTime}";

        return ParseDateTimeOffset(composite);
    }
}
