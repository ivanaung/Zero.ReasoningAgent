using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class ProjectComment
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    [MaxLength(120)]
    public string AuthorId { get; set; } = "anonymous";

    [MaxLength(120)]
    public string AuthorName { get; set; } = "Anonymous";

    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
