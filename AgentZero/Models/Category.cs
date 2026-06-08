using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class Category
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Color { get; set; } // Hex color code
}
