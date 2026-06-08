using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public enum EventStatus
{
    Todo,
    InProgress,
    Done,
    Cancelled,
    Blocked
}

public enum EventPriority
{
    Low,
    Medium,
    High,
    Critical
}

public class ScheduleEvent
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [Required]
    public DateTime StartDateTime { get; set; }
    
    [Required]
    public DateTime EndDateTime { get; set; }
    
    public bool IsAllDay { get; set; } = false;
    
    public EventStatus Status { get; set; } = EventStatus.Todo;
    
    public EventPriority Priority { get; set; } = EventPriority.Medium;
    
    public int Progress { get; set; } = 0; // 0-100 percentage
    
    public string? Icon { get; set; }           // Emoji or icon class (e.g. 🚀)
    
    public int? CategoryId { get; set; }
    public virtual Category? Category { get; set; }

    public int? ProjectId { get; set; }
    public virtual Project? Project { get; set; }

    public int? StageId { get; set; }
    public virtual ProjectStage? Stage { get; set; }
    
    public int? DependsOnId { get; set; }
    public virtual ScheduleEvent? DependsOn { get; set; }

    public bool IsTodoListTask { get; set; } = false;

    public string? AreaName { get; set; }      // For grouping in Timeline (e.g. Front-end, Back-end)

    public string? Color { get; set; }         // hex color

    [MaxLength(120)]
    public string? AssignedTo { get; set; }
    
    public bool IsRecurring { get; set; } = false;
    public string? RecurrenceRule { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // Derived Properties
    public bool IsOverdue => Status != EventStatus.Done && EndDateTime < DateTime.Now;
}
