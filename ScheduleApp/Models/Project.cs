using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public enum ProjectStatus
{
    Active,
    OnHold,
    Completed,
    Maintenance,
    Archived
}

public class Project
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public string? Color { get; set; } = "#2E5DA6";

    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    public virtual ICollection<ScheduleEvent> Events { get; set; } = new List<ScheduleEvent>();
}
