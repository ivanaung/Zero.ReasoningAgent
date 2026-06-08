using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

public class EventsController(
    IEventService eventService,
    ICategoryService categoryService,
    ISettingsService settingsService,
    IProjectService projectService,
    IProjectStageService projectStageService,
    IGoogleIntegrationService googleIntegrationService) : Controller
{
    private bool IsModalRequest()
    {
        if (string.Equals(Request.Query["modal"], "1", StringComparison.Ordinal))
        {
            return true;
        }

        if (Request.HasFormContentType && string.Equals(Request.Form["modal"], "1", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult EventFormResponse(string viewName, EventFormViewModel model)
    {
        ViewBag.IsModal = IsModalRequest();
        return ViewBag.IsModal ? PartialView("_EventForm", model) : View(viewName, model);
    }

    private async Task LoadSelectData(int? categoryId = null, int? projectId = null, int? stageId = null, int? dependsOnId = null)
    {
        var projects = await projectService.GetAllAsync();
        var stages = await projectStageService.GetAllAsync();

        ViewBag.Categories = new SelectList(await categoryService.GetAllAsync(), "Id", "Name", categoryId);
        ViewBag.Projects = new SelectList(projects, "Id", "Name", projectId);
        ViewBag.ProjectStages = stages;
        ViewBag.SelectedStageId = stageId;

        var allEvents = await eventService.GetAllAsync();
        ViewBag.AllEvents = new SelectList(allEvents, "Id", "Title", dependsOnId);
    }

    private async Task LoadUpcomingEventsAsync()
    {
        var allEvents = await eventService.GetAllAsync();
        ViewBag.UpcomingEvents = allEvents
            .Where(e => e.EndDateTime >= DateTime.Now && e.Status != EventStatus.Done && !e.IsTodoListTask)
            .OrderBy(e => e.StartDateTime)
            .Take(8)
            .Select(e => new AgendaItemViewModel
            {
                Id = e.Id,
                Title = e.Title,
                TimeLabel = e.IsAllDay
                    ? $"{e.StartDateTime:ddd dd MMM} · All day"
                    : $"{e.StartDateTime:ddd dd MMM} · {e.StartDateTime:HH:mm} - {e.EndDateTime:HH:mm}",
                Status = e.Status.ToString(),
                Color = GetEventDotColor(e),
                IsCritical = e.Priority == EventPriority.Critical,
                Icon = e.Icon
            })
            .ToList();

        var yesterday = DateTime.Now.AddHours(-24);
        ViewBag.ToDoTasks = allEvents
            .Where(e => e.IsTodoListTask && (e.Status != EventStatus.Done || (e.EndDateTime >= yesterday)))
            .OrderBy(e => e.Status == EventStatus.Done)
            .ThenBy(e => e.StartDateTime)
            .Select(e => new AgendaItemViewModel
            {
                Id = e.Id,
                Title = e.Title,
                TimeLabel = e.IsAllDay ? $"{e.StartDateTime:MMM dd}" : $"{e.StartDateTime:MMM dd HH:mm}",
                Status = e.Status.ToString(),
                Color = GetEventDotColor(e),
                IsCritical = e.Priority == EventPriority.Critical,
                IsDone = e.Status == EventStatus.Done,
                Icon = e.Icon
            })
            .ToList();
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? date, string? view)
    {
        var focusDate = date ?? DateTime.Today;
        var settings = await settingsService.GetSettingsAsync();

        ViewBag.InitialDate = focusDate.ToString("yyyy-MM-dd");
        ViewBag.InitialView = string.IsNullOrWhiteSpace(view) ? "dayGridMonth" : view;
        ViewBag.ShowFullDay = settings.ShowFullDayInCalendar;
        ViewBag.StartTime = settings.DayStartTime;
        ViewBag.EndTime = settings.DayEndTime;
        await LoadUpcomingEventsAsync();

        return View(focusDate);
    }

    [HttpGet]
    public async Task<IActionResult> JsonList(DateTime start, DateTime end)
    {
        var events = await eventService.GetEventsInRangeAsync(start, end);
        var result = events.Select(e => new
        {
            id = e.Id,
            title = e.Title,
            start = e.StartDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
            end = e.EndDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
            allDay = e.IsAllDay,
            color = GetEventColor(e),
            extendedProps = new
            {
                dotColor = GetEventDotColor(e),
                status = e.Status.ToString(),
                priority = e.Priority.ToString(),
                category = e.Category?.Name,
                project = e.Project?.Name,
                icon = e.Icon,
                progress = e.Progress,
                dependsOnId = e.DependsOnId,
                areaName = e.Stage?.Name ?? e.AreaName ?? e.Category?.Name ?? "Unassigned",
                stage = e.Stage?.Name ?? e.AreaName ?? e.Category?.Name ?? "Unassigned",
                isOverdue = e.IsOverdue
            }
        }).ToList<object>();

        try
        {
            var linkedCalendarEvents = await googleIntegrationService.GetUpcomingCalendarEventsAsync(
                new DateTimeOffset(start),
                new DateTimeOffset(end),
                500,
                HttpContext.RequestAborted);

            result.AddRange(linkedCalendarEvents.Select(item => new
            {
                id = $"google-{item.Id}",
                title = item.Title,
                start = item.Start?.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = item.End?.ToString("yyyy-MM-ddTHH:mm:ss"),
                allDay = item.Start.HasValue
                    && item.End.HasValue
                    && item.Start.Value.TimeOfDay == TimeSpan.Zero
                    && item.End.Value.TimeOfDay == TimeSpan.Zero,
                color = "#34a853",
                editable = false,
                extendedProps = new
                {
                    dotColor = "#34a853",
                    status = "Linked",
                    priority = "External",
                    category = "Google Calendar",
                    project = "Linked Calendar",
                    icon = "G",
                    progress = 0,
                    dependsOnId = (int?)null,
                    areaName = "Google Calendar",
                    stage = "Google Calendar",
                    isOverdue = false,
                    source = "google-calendar",
                    htmlLink = item.HtmlLink,
                    organizer = item.Organizer
                }
            }));
        }
        catch
        {
            // Keep the primary calendar usable even when Google Calendar is unavailable.
        }

        return Json(result);
    }

    public async Task<IActionResult> Day(DateTime? date, bool showFullDay = false)
    {
        var dayDate = date ?? DateTime.Today;
        var events = await eventService.GetEventsForDayAsync(dayDate);
        var settings = await settingsService.GetSettingsAsync();

        ViewBag.ShowFullDay = showFullDay || settings.ShowFullDayInCalendar;
        ViewBag.StartTime = settings.DayStartTime;
        ViewBag.EndTime = settings.DayEndTime;
        await LoadUpcomingEventsAsync();

        return View(new DayViewModel { Date = dayDate, Events = events });
    }

    [HttpGet]
    public async Task<IActionResult> Create(DateTime? date)
    {
        var start = date ?? DateTime.Today;
        if (date.HasValue && date.Value.Hour == 0 && date.Value.Minute == 0)
        {
            var settings = await settingsService.GetSettingsAsync();
            if (TimeSpan.TryParse(settings.DayStartTime, out var startTime))
            {
                start = date.Value.Date.Add(startTime);
            }
            else
            {
                start = date.Value.Date.AddHours(8);
            }
        }

        await LoadSelectData(projectId: null, stageId: null);

        return EventFormResponse(nameof(Create), new EventFormViewModel
        {
            StartDateTime = start,
            EndDateTime = start.AddHours(1)
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateModal(DateTime? date)
    {
        return await Create(date);
    }

    [HttpGet]
    public async Task<IActionResult> EditModal(int id)
    {
        return await Edit(id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            int? stageId = model.StageId;
            string? areaName = null;

            if (model.StageId.HasValue)
            {
                var stage = await projectStageService.GetByIdAsync(model.StageId.Value);
                stageId = stage?.Id;
                areaName = stage?.Name;
            }

            var evt = new ScheduleEvent
            {
                Title = model.Title,
                Description = model.Description,
                StartDateTime = model.StartDateTime,
                EndDateTime = model.EndDateTime,
                IsAllDay = model.IsAllDay,
                Status = model.Status,
                Priority = model.Priority,
                CategoryId = model.CategoryId,
                ProjectId = model.ProjectId,
                StageId = stageId,
                DependsOnId = model.DependsOnId,
                AreaName = areaName,
                Progress = model.Progress,
                IsTodoListTask = model.IsTodoListTask,
                Icon = model.Icon,
                Color = model.Color,
                IsRecurring = model.IsRecurring,
                RecurrenceRule = model.RecurrenceRule
            };

            await eventService.CreateAsync(evt);
            if (IsModalRequest())
            {
                return Json(new { success = true });
            }

            return RedirectToAction(nameof(Day), new { date = evt.StartDateTime.ToString("yyyy-MM-dd") });
        }

        await LoadSelectData(model.CategoryId, model.ProjectId, model.StageId, model.DependsOnId);
        return EventFormResponse(nameof(Create), model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var evt = await eventService.GetByIdAsync(id);
        if (evt == null) return NotFound();

        await LoadSelectData(evt.CategoryId, evt.ProjectId, evt.StageId, evt.DependsOnId);

        return EventFormResponse(nameof(Edit), new EventFormViewModel
        {
            Id = evt.Id,
            Title = evt.Title,
            Description = evt.Description,
            StartDateTime = evt.StartDateTime,
            EndDateTime = evt.EndDateTime,
            IsAllDay = evt.IsAllDay,
            Status = evt.Status,
            Priority = evt.Priority,
            Progress = evt.Progress,
            CategoryId = evt.CategoryId,
            ProjectId = evt.ProjectId,
            StageId = evt.StageId,
            DependsOnId = evt.DependsOnId,
            AreaName = evt.AreaName,
            IsTodoListTask = evt.IsTodoListTask,
            Icon = evt.Icon,
            Color = evt.Color,
            IsRecurring = evt.IsRecurring,
            RecurrenceRule = evt.RecurrenceRule
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EventFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var evt = await eventService.GetByIdAsync(model.Id);
            if (evt == null) return NotFound();

            int? stageId = model.StageId;
            string? areaName = evt.AreaName;

            if (model.StageId.HasValue)
            {
                var stage = await projectStageService.GetByIdAsync(model.StageId.Value);
                stageId = stage?.Id;
                areaName = stage?.Name;
            }
            else if (!model.ProjectId.HasValue)
            {
                stageId = null;
                areaName = null;
            }
            else if (evt.StageId.HasValue && !model.StageId.HasValue)
            {
                areaName = evt.Stage?.Name ?? evt.AreaName;
            }

            evt.Title = model.Title;
            evt.Description = model.Description;
            evt.StartDateTime = model.StartDateTime;
            evt.EndDateTime = model.EndDateTime;
            evt.IsAllDay = model.IsAllDay;
            evt.Status = model.Status;
            evt.Priority = model.Priority;
            evt.Progress = model.Progress;
            evt.CategoryId = model.CategoryId;
            evt.ProjectId = model.ProjectId;
            evt.StageId = stageId;
            evt.DependsOnId = model.DependsOnId;
            evt.AreaName = areaName;
            evt.IsTodoListTask = model.IsTodoListTask;
            evt.Icon = model.Icon;
            evt.Color = model.Color;
            evt.IsRecurring = model.IsRecurring;
            evt.RecurrenceRule = model.RecurrenceRule;

            await eventService.UpdateAsync(evt);
            if (IsModalRequest())
            {
                return Json(new { success = true });
            }

            return RedirectToAction(nameof(Day), new { date = evt.StartDateTime.ToString("yyyy-MM-dd") });
        }

        await LoadSelectData(model.CategoryId, model.ProjectId, model.StageId, model.DependsOnId);
        return EventFormResponse(nameof(Edit), model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var evt = await eventService.GetByIdAsync(id);
        if (evt == null) return NotFound();
        
        var date = evt.StartDateTime;
        await eventService.DeleteAsync(id);

        if (IsModalRequest())
        {
            return Json(new { success = true });
        }

        return RedirectToAction(nameof(Day), new { date = date.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, EventStatus status)
    {
        await eventService.UpdateStatusAsync(id, status);
        return Ok();
    }

    private string GetEventDotColor(ScheduleEvent e)
    {
        if (e.Status == EventStatus.Done) return "#27AE60";
        if (e.Status == EventStatus.Blocked) return "#922B21";

        if (!string.IsNullOrEmpty(e.Color) && e.Color != "#000000" && e.Color != "#2E5DA6") return e.Color;
        if (e.Category != null && !string.IsNullOrEmpty(e.Category.Color)) return e.Category.Color;

        return "#2E5DA6";
    }

    private string GetEventColor(ScheduleEvent e)
    {
        if (e.Priority == EventPriority.Critical) return "#C0392B";
        return GetEventDotColor(e);
    }
}
