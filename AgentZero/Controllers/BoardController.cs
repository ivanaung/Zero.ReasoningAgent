using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Models;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

public class BoardController(IEventService eventService, IProjectService projectService, IProjectStageService projectStageService) : Controller
{
    public async Task<IActionResult> Index(int? projectId, int? stageId)
    {
        var projects = await projectService.GetAllAsync();
        
        // Default to first active project if none selected
        if (!projectId.HasValue && projects.Any())
        {
            projectId = projects.First().Id;
        }

        var events = projectId.HasValue 
            ? await eventService.GetEventsByProjectAsync(projectId.Value)
            : await eventService.GetAllAsync();

        if (stageId.HasValue)
        {
            events = events.Where(evt => evt.StageId == stageId.Value).ToList();
        }

        ViewBag.SelectedProjectId = projectId;
        ViewBag.Projects = projects;
        ViewBag.SelectedProject = projects.FirstOrDefault(p => p.Id == projectId);
        ViewBag.SelectedStageId = stageId;
        ViewBag.Stages = (await projectStageService.GetAllAsync()) ?? new List<ProjectStage>();

        return View(events);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateEventStatus(int eventId, EventStatus status)
    {
        await eventService.UpdateStatusAsync(eventId, status);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateEventProgress(int eventId, int progress)
    {
        var evt = await eventService.GetByIdAsync(eventId);
        if (evt != null)
        {
            evt.Progress = progress;
            await eventService.UpdateAsync(evt);
            return Json(new { success = true });
        }
        return NotFound();
    }
}
