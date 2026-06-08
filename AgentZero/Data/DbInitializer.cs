using Microsoft.EntityFrameworkCore;
using ScheduleApp.Models;

namespace ScheduleApp.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        context.Database.EnsureCreated();
        EnsureProjectStageSchema(context);
        MigrateLegacyAreasToStages(context);

        if (context.Categories.Any())
        {
            return;
        }

        var categories = new Category[]
        {
            new Category { Name = "Work", Color = "#2E5DA6" },
            new Category { Name = "Personal", Color = "#9B59B6" },
            new Category { Name = "Meeting", Color = "#E67E22" },
            new Category { Name = "Design", Color = "#C0392B" },
            new Category { Name = "Success", Color = "#27AE60" }
        };

        context.Categories.AddRange(categories);
        context.SaveChanges();

        var workCat = categories[0];
        var meetingCat = categories[2];
        var designCat = categories[3];
        var planningCat = categories[4];

        var events = new ScheduleEvent[]
        {
            new ScheduleEvent
            {
                Title = "Project Progress Review",
                Description = "Weekly sync with the engineering team to review SCADA project milestones.",
                StartDateTime = DateTime.Today.AddHours(10),
                EndDateTime = DateTime.Today.AddHours(11),
                Status = EventStatus.InProgress,
                Priority = EventPriority.High,
                CategoryId = meetingCat.Id,
                Icon = "📊"
            },
            new ScheduleEvent
            {
                Title = "Team Lunch",
                Description = "Casual lunch at the nearby bistro.",
                StartDateTime = DateTime.Today.AddHours(12),
                EndDateTime = DateTime.Today.AddHours(13),
                Status = EventStatus.Todo,
                Priority = EventPriority.Medium,
                CategoryId = workCat.Id,
                Icon = "🍔"
            },
            new ScheduleEvent
            {
                Title = "UI Design System Polish",
                Description = "Finalize the border radius and shadow tokens for the new dashboard.",
                StartDateTime = DateTime.Today.AddHours(14),
                EndDateTime = DateTime.Today.AddHours(16),
                Status = EventStatus.Todo,
                Priority = EventPriority.Critical,
                CategoryId = designCat.Id,
                Icon = "🎨"
            },
            new ScheduleEvent
            {
                Title = "Daily Standup",
                StartDateTime = DateTime.Today.AddDays(1).AddHours(9),
                EndDateTime = DateTime.Today.AddDays(1).AddHours(9).AddMinutes(30),
                Status = EventStatus.Todo,
                Priority = EventPriority.Medium,
                CategoryId = meetingCat.Id,
                Icon = "🌅"
            },
            new ScheduleEvent
            {
                Title = "Release V1.0 Planning",
                Description = "Strategic planning for the initial release phase.",
                StartDateTime = DateTime.Today.AddDays(-1).AddHours(11),
                EndDateTime = DateTime.Today.AddDays(-1).AddHours(12),
                Status = EventStatus.Done,
                Priority = EventPriority.High,
                CategoryId = planningCat.Id,
                Icon = "🚀"
            }
        };

        context.Events.AddRange(events);
        context.SaveChanges();
    }

    private static void EnsureProjectStageSchema(AppDbContext context)
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS ProjectStages (
                Id INTEGER NOT NULL CONSTRAINT PK_ProjectStages PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_ProjectStages_Name
            ON ProjectStages (Name);
            """);

        var eventColumns = context.Database
            .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Events');")
            .ToList();

        if (!eventColumns.Contains("StageId", StringComparer.OrdinalIgnoreCase))
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Events ADD COLUMN StageId INTEGER NULL;");
            context.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Events_StageId ON Events (StageId);");
        }

        if (!eventColumns.Contains("IsTodoListTask", StringComparer.OrdinalIgnoreCase))
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE Events ADD COLUMN IsTodoListTask INTEGER NOT NULL DEFAULT 0;");
        }
    }

    private static void MigrateLegacyAreasToStages(AppDbContext context)
    {
        var legacyEvents = context.Events
            .AsEnumerable()
            .Where(evt => !string.IsNullOrWhiteSpace(evt.AreaName))
            .ToList();

        if (legacyEvents.Count == 0)
        {
            return;
        }

        var changed = false;

        foreach (var stageGroup in legacyEvents.GroupBy(evt => evt.AreaName!.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var stage = context.ProjectStages
                .FirstOrDefault(existing => existing.Name == stageGroup.Key);

            if (stage == null)
            {
                stage = new ProjectStage
                {
                    Name = stageGroup.Key,
                    CreatedAt = DateTime.Now
                };
                context.ProjectStages.Add(stage);
                context.SaveChanges();
            }

            foreach (var evt in stageGroup)
            {
                if (evt.StageId == stage.Id)
                {
                    continue;
                }

                evt.StageId = stage.Id;
                changed = true;
            }
        }

        if (changed)
        {
            context.SaveChanges();
        }
    }
}
