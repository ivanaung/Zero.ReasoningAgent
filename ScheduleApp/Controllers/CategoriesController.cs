using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

public class CategoriesController(
    ICategoryService categoryService,
    IProjectService projectService,
    IProjectStageService projectStageService) : Controller
{
    public async Task<IActionResult> Index(string? tab = null)
    {
        return View(new MetadataIndexViewModel
        {
            Categories = await categoryService.GetAllAsync(),
            Projects = await projectService.GetAllAsync(),
            Stages = await projectStageService.GetAllAsync(),
            ActiveTab = NormalizeTab(tab)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        if (ModelState.IsValid)
        {
            await categoryService.CreateAsync(category);
        }
        return RedirectToAction(nameof(Index), new { tab = "categories" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Category category)
    {
        if (ModelState.IsValid)
        {
            await categoryService.UpdateAsync(category);
        }
        return RedirectToAction(nameof(Index), new { tab = "categories" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await categoryService.DeleteAsync(id);
        return RedirectToAction(nameof(Index), new { tab = "categories" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStage(ProjectStageFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            await projectStageService.CreateAsync(new ProjectStage
            {
                Name = model.Name,
                Description = model.Description
            });
        }

        return RedirectToAction(nameof(Index), new { tab = "stages" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditStage(ProjectStageFormViewModel model)
    {
        if (ModelState.IsValid && model.Id > 0)
        {
            await projectStageService.UpdateAsync(new ProjectStage
            {
                Id = model.Id,
                Name = model.Name,
                Description = model.Description
            });
        }

        return RedirectToAction(nameof(Index), new { tab = "stages" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStage(int id)
    {
        await projectStageService.DeleteAsync(id);
        return RedirectToAction(nameof(Index), new { tab = "stages" });
    }

    private static string NormalizeTab(string? tab)
    {
        return string.Equals(tab, "stages", StringComparison.OrdinalIgnoreCase)
            ? "stages"
            : "categories";
    }
}
