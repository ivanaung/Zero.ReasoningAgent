using ScheduleApp.Models;

namespace ScheduleApp.Models.ViewModels;

public class MetadataIndexViewModel
{
    public IReadOnlyList<Category> Categories { get; init; } = [];

    public IReadOnlyList<ProjectStage> Stages { get; init; } = [];

    public IReadOnlyList<Project> Projects { get; init; } = [];

    public string ActiveTab { get; init; } = "categories";
}
