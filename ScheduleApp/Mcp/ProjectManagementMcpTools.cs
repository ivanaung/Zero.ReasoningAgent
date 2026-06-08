using System.ComponentModel;
using ModelContextProtocol.Server;
using ScheduleApp.Services;

namespace ScheduleApp.Mcp;

[McpServerToolType]
public sealed class ProjectManagementMcpTools(IProjectManagementToolService toolService)
{
    [McpServerTool, Description("Returns the current task and status summary for a project.")]
    public Task<string> GetProjectSummary(
        [Description("The project identifier.")] int projectId,
        CancellationToken cancellationToken = default) =>
        toolService.GetProjectSummary(projectId, cancellationToken);

    [McpServerTool, Description("Returns full detail for a single task.")]
    public Task<string> GetTaskSummary(
        [Description("The task identifier.")] int taskId,
        CancellationToken cancellationToken = default) =>
        toolService.GetTaskSummary(taskId, cancellationToken);

    [McpServerTool, Description("Lists tasks for a project.")]
    public Task<string> GetTasksByProject(
        [Description("The project identifier.")] int projectId,
        CancellationToken cancellationToken = default) =>
        toolService.GetTasksByProject(projectId, cancellationToken);

    [McpServerTool, Description("Lists overdue tasks globally or for a specific project.")]
    public Task<string> GetOverdueTasks(
        [Description("Optional project identifier.")] int? projectId = null,
        CancellationToken cancellationToken = default) =>
        toolService.GetOverdueTasks(projectId, cancellationToken);

    [McpServerTool, Description("Lists blocked tasks globally or for a specific project.")]
    public Task<string> GetBlockedTasks(
        [Description("Optional project identifier.")] int? projectId = null,
        CancellationToken cancellationToken = default) =>
        toolService.GetBlockedTasks(projectId, cancellationToken);

    [McpServerTool, Description("Lists upcoming critical milestones.")]
    public Task<string> GetUpcomingMilestones(
        [Description("Optional project identifier.")] int? projectId = null,
        CancellationToken cancellationToken = default) =>
        toolService.GetUpcomingMilestones(projectId, cancellationToken);

    [McpServerTool, Description("Finds projects by name or description.")]
    public Task<string> SearchProjects(
        [Description("The project search query.")] string query,
        CancellationToken cancellationToken = default) =>
        toolService.SearchProjects(query, cancellationToken);

    [McpServerTool, Description("Finds tasks by title, description, or project context.")]
    public Task<string> SearchTasks(
        [Description("The task search query.")] string query,
        CancellationToken cancellationToken = default) =>
        toolService.SearchTasks(query, cancellationToken);

    [McpServerTool, Description("Finds the single best task match by name or short title.")]
    public Task<string> FindTaskByName(
        [Description("The task name or short title, for example 'Rejo'.")] string name,
        CancellationToken cancellationToken = default) =>
        toolService.FindTaskByName(name, cancellationToken);

    [McpServerTool, Description("Creates a new top-level project.")]
    public Task<string> CreateProject(
        [Description("The project name.")] string name,
        [Description("Optional project description.")] string? description = null,
        [Description("Optional project color in hex format.")] string? color = null,
        CancellationToken cancellationToken = default) =>
        toolService.CreateProject(name, description, color, cancellationToken);

    [McpServerTool, Description("Creates a task through the scheduling rules.")]
    public Task<string> CreateTask(
        [Description("The task title.")] string title,
        [Description("Optional task description.")] string? description = null,
        [Description("Optional project identifier.")] int? projectId = null,
        [Description("Optional project name.")] string? projectName = null,
        [Description("Optional dependency task identifier.")] int? dependsOnTaskId = null,
        [Description("Optional category identifier.")] int? categoryId = null,
        [Description("Optional stage identifier.")] int? stageId = null,
        [Description("Optional stage name.")] string? stageName = null,
        [Description("Optional assignee label.")] string? assignee = null,
        [Description("Optional preferred start date/time.")] string? startHint = null,
        [Description("Optional preferred end date/time.")] string? endHint = null,
        [Description("Whether the task is all-day.")] bool? allDay = null,
        CancellationToken cancellationToken = default) =>
        toolService.CreateTask(title, description, projectId, projectName, dependsOnTaskId, categoryId, stageId, stageName, assignee, startHint, endHint, allDay, cancellationToken);

    [McpServerTool, Description("Reschedules an existing task.")]
    public Task<string> UpdateTaskDates(
        [Description("The task identifier.")] int taskId,
        [Description("Optional new start date/time.")] string? startDateTime = null,
        [Description("Optional new end date/time.")] string? endDateTime = null,
        [Description("Reason for the schedule change.")] string? reason = null,
        CancellationToken cancellationToken = default) =>
        toolService.UpdateTaskDates(taskId, startDateTime, endDateTime, reason, cancellationToken);

    [McpServerTool, Description("Assigns a task to a user label.")]
    public Task<string> AssignTask(
        [Description("The task identifier.")] int taskId,
        [Description("The assignee label.")] string assignee,
        CancellationToken cancellationToken = default) =>
        toolService.AssignTask(taskId, assignee, cancellationToken);

    [McpServerTool, Description("Adds a comment to a task.")]
    public Task<string> AddTaskComment(
        [Description("The task identifier.")] int taskId,
        [Description("The comment content.")] string comment,
        CancellationToken cancellationToken = default) =>
        toolService.AddTaskComment(taskId, comment, cancellationToken);

    [McpServerTool, Description("Adds a comment to a project.")]
    public Task<string> AddProjectComment(
        [Description("The project identifier.")] int projectId,
        [Description("The comment content.")] string comment,
        CancellationToken cancellationToken = default) =>
        toolService.AddProjectComment(projectId, comment, cancellationToken);

    [McpServerTool, Description("Shows scheduled work for a user or the team over a date range.")]
    public Task<string> GetTeamAvailability(
        [Description("Optional user identifier.")] string? userId = null,
        [Description("Optional start date/time.")] string? from = null,
        [Description("Optional end date/time.")] string? to = null,
        CancellationToken cancellationToken = default) =>
        toolService.GetTeamAvailability(userId, from, to, cancellationToken);

    [McpServerTool, Description("Lists assigned tasks for one user in a time window.")]
    public Task<string> GetUserAssignedTasks(
        [Description("The user identifier.")] string userId,
        [Description("Optional start date/time.")] string? from = null,
        [Description("Optional end date/time.")] string? to = null,
        CancellationToken cancellationToken = default) =>
        toolService.GetUserAssignedTasks(userId, from, to, cancellationToken);

    [McpServerTool, Description("Lists tasks due soon for one user.")]
    public Task<string> GetDueSoonTasks(
        [Description("The user identifier.")] string userId,
        [Description("Optional start date/time.")] string? from = null,
        [Description("Optional end date/time.")] string? to = null,
        CancellationToken cancellationToken = default) =>
        toolService.GetDueSoonTasks(userId, from, to, cancellationToken);

    [McpServerTool, Description("Lists blocked or overdue project risk items.")]
    public Task<string> GetProjectRisks(
        [Description("Optional user identifier.")] string? userId = null,
        [Description("Optional project identifier.")] int? projectId = null,
        CancellationToken cancellationToken = default) =>
        toolService.GetProjectRisks(userId, projectId, cancellationToken);

    [McpServerTool, Description("Returns assignment context for a user calendar window.")]
    public Task<string> GetCalendarContext(
        [Description("The user identifier.")] string userId,
        [Description("Optional start date/time.")] string? from = null,
        [Description("Optional end date/time.")] string? to = null,
        CancellationToken cancellationToken = default) =>
        toolService.GetCalendarContext(userId, from, to, cancellationToken);

    [McpServerTool, Description("Returns the status of the linked Google email and calendar integration.")]
    public Task<string> GetGoogleIntegrationStatus(CancellationToken cancellationToken = default) =>
        toolService.GetGoogleIntegrationStatus(cancellationToken);

    [McpServerTool, Description("Returns a recent summary of linked Gmail inbox messages.")]
    public Task<string> GetInboxSummary(
        [Description("Maximum number of inbox messages to return.")] int maxItems = 10,
        CancellationToken cancellationToken = default) =>
        toolService.GetInboxSummary(maxItems, cancellationToken);

    [McpServerTool, Description("Searches linked Gmail inbox messages by keyword.")]
    public Task<string> SearchInboxEmails(
        [Description("The inbox search query.")] string query,
        [Description("Maximum number of messages to return.")] int maxItems = 5,
        CancellationToken cancellationToken = default) =>
        toolService.SearchInboxEmails(query, maxItems, cancellationToken);

    [McpServerTool, Description("Returns upcoming events from the linked Google Calendar account.")]
    public Task<string> GetLinkedCalendarEvents(
        [Description("Optional start date/time.")] string? from = null,
        [Description("Optional end date/time.")] string? to = null,
        [Description("Maximum number of events to return.")] int maxItems = 10,
        CancellationToken cancellationToken = default) =>
        toolService.GetLinkedCalendarEvents(from, to, maxItems, cancellationToken);

    [McpServerTool, Description("Calculates the next valid working slot using the configured calendar rules.")]
    public Task<string> CalculateNextWorkingSlot(
        [Description("Optional baseline date/time.")] string? after = null,
        [Description("Duration in minutes.")] int durationMinutes = 60,
        [Description("Optional preferred date.")] string? preferredDate = null,
        [Description("Optional preferred time.")] string? preferredTime = null,
        CancellationToken cancellationToken = default) =>
        toolService.CalculateNextWorkingSlot(after, durationMinutes, preferredDate, preferredTime, cancellationToken);

    [McpServerTool, Description("Returns the dependency chain for a task.")]
    public Task<string> GetDependencyChain(
        [Description("The task identifier.")] int taskId,
        CancellationToken cancellationToken = default) =>
        toolService.GetDependencyChain(taskId, cancellationToken);

    [McpServerTool, Description("Returns the current server date and time.")]
    public string GetCurrentDateTime() => toolService.GetCurrentDateTime();

    [McpServerTool, Description("Returns timezone, hours, and working-day rules.")]
    public Task<string> GetWorkingCalendarRules(CancellationToken cancellationToken = default) =>
        toolService.GetWorkingCalendarRules(cancellationToken);

    [McpServerTool, Description("Creates a follow-up task from an existing task.")]
    public Task<string> CreateFollowUpTask(
        [Description("The source task identifier.")] int taskId,
        [Description("The follow-up task title.")] string title,
        [Description("Optional assignee label.")] string? assignee = null,
        CancellationToken cancellationToken = default) =>
        toolService.CreateFollowUpTask(taskId, title, assignee, cancellationToken);

    [McpServerTool, Description("Creates a manual notification queue entry.")]
    public Task<string> CreateNotificationScheduleEntry(
        [Description("The target user identifier.")] string userId,
        [Description("Notification type name.")] string notificationType,
        [Description("Scheduled UTC date/time.")] string scheduledForUtc,
        [Description("Optional message body.")] string? message = null,
        [Description("Optional task identifier.")] int? taskId = null,
        [Description("Optional project identifier.")] int? projectId = null,
        CancellationToken cancellationToken = default) =>
        toolService.CreateNotificationScheduleEntry(userId, notificationType, scheduledForUtc, message, taskId, projectId, cancellationToken);

    [McpServerTool, Description("Cancels pending notifications for a task.")]
    public Task<string> CancelNotificationScheduleEntriesForTask(
        [Description("The task identifier.")] int taskId,
        CancellationToken cancellationToken = default) =>
        toolService.CancelNotificationScheduleEntriesForTask(taskId, cancellationToken);

    [McpServerTool, Description("Rebuilds pending notifications for a task.")]
    public Task<string> RecomputeNotificationPlanForTask(
        [Description("The task identifier.")] int taskId,
        CancellationToken cancellationToken = default) =>
        toolService.RecomputeNotificationPlanForTask(taskId, cancellationToken);

    [McpServerTool, Description("Returns context for a notification schedule entry.")]
    public Task<string> GetNotificationContext(
        [Description("The notification identifier.")] int notificationId,
        CancellationToken cancellationToken = default) =>
        toolService.GetNotificationContext(notificationId, cancellationToken);

    [McpServerTool, Description("Converts time to a target city or timezone.")]
    public string ConvertTime(
        [Description("The target location or timezone.")] string targetTimeZone,
        [Description("Optional time to convert.")] string? timeToConvert = null) =>
        toolService.ConvertTime(targetTimeZone, timeToConvert);
}
