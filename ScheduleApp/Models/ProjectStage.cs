using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class ProjectStage
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual ICollection<ScheduleEvent> Events { get; set; } = new List<ScheduleEvent>();
}
