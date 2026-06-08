namespace ScheduleApp.Models;

public class OperationalScheduleEvent
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public bool IsAllDay { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Todo;

    public EventPriority Priority { get; set; } = EventPriority.Medium;

    public int Progress { get; set; }

    public string? Icon { get; set; }

    public int? CategoryId { get; set; }

    public int? ProjectId { get; set; }

    public int? StageId { get; set; }

    public int? DependsOnId { get; set; }

    public bool IsTodoListTask { get; set; }

    public string? AreaName { get; set; }

    public string? Color { get; set; }

    public string? AssignedTo { get; set; }

    public bool IsRecurring { get; set; }

    public string? RecurrenceRule { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
