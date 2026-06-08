using System.ComponentModel.DataAnnotations;
using ScheduleApp.Models;

namespace ScheduleApp.Models.ViewModels;

public class EventFormViewModel
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

    public bool IsAllDay { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Todo;

    public EventPriority Priority { get; set; } = EventPriority.Medium;

    [Range(0, 100)]
    public int Progress { get; set; }

    public int? CategoryId { get; set; }

    public int? ProjectId { get; set; }

    public int? StageId { get; set; }

    public int? DependsOnId { get; set; }

    public string? AreaName { get; set; }

    [Display(Name = "Track in standalone To-Do List")]
    public bool IsTodoListTask { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    [MaxLength(20)]
    public string? Color { get; set; }

    public bool IsRecurring { get; set; }

    public string? RecurrenceRule { get; set; }
}
