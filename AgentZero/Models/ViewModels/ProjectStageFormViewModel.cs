using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models.ViewModels;

public class ProjectStageFormViewModel
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
