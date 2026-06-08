namespace ScheduleApp.Models.ViewModels;

public class DashboardViewModel
{
    public int ActiveProjects { get; set; }
    public int OpenTasks { get; set; }
    public int CompletedToday { get; set; }
    public int OverdueTasks { get; set; }
    public int? SelectedProjectId { get; set; }
    public string SelectedProjectName { get; set; } = "All Projects";
    public List<ProjectProgressItemViewModel> ProjectProgress { get; set; } = new();
    public List<ProjectOptionItemViewModel> Projects { get; set; } = new();
    public List<StageProgressItemViewModel> StageProgress { get; set; } = new();
    public List<StatusCountItemViewModel> StatusBreakdown { get; set; } = new();
    public List<AgendaItemViewModel> UpcomingTasks { get; set; } = new();
    public List<AgendaItemViewModel> ToDoTasks { get; set; } = new();
    public List<PriorityCountItemViewModel> PriorityBreakdown { get; set; } = new();
    public RecommendationResultViewModel? NextHourFocus { get; set; }
    public RecommendationResultViewModel? TomorrowPreview { get; set; }
    public List<NotificationCenterItemViewModel> UpcomingReminders { get; set; } = new();
    public FinanceSummaryWidgetViewModel? FinanceSummary { get; set; }
}

public class ProjectProgressItemViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string Color { get; set; } = "#2E5DA6";
}

public class StatusCountItemViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ProjectOptionItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class StageProgressItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Progress { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public string Color { get; set; } = "#2E5DA6";
}

public class PriorityCountItemViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AgendaItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TimeLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Color { get; set; } = "#2E5DA6";
    public bool IsCritical { get; set; }
    public bool IsDone { get; set; }
    public string? Icon { get; set; }
}
