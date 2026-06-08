using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Models;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

public class ProjectsController(IProjectService projectService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var projects = await projectService.GetAllAsync();
        return View(projects);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Project project)
    {
        if (ModelState.IsValid)
        {
            await projectService.CreateAsync(project);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Project project)
    {
        if (ModelState.IsValid && project.Id > 0)
        {
            await projectService.UpdateAsync(project);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await projectService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, ProjectStatus status)
    {
        var project = await projectService.GetByIdAsync(id);
        if (project != null)
        {
            project.Status = status;
            await projectService.UpdateAsync(project);
            return Ok();
        }
        return NotFound();
    }
}
