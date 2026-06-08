using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IProjectService
{
    Task<List<Project>> GetAllAsync();
    Task<Project?> GetByIdAsync(int id);
    Task CreateAsync(Project project);
    Task UpdateAsync(Project project);
    Task DeleteAsync(int id);
}

public class ProjectService(AppDbContext context) : IProjectService
{
    public async Task<List<Project>> GetAllAsync()
    {
        return await context.Projects
            .Include(p => p.Events)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Project?> GetByIdAsync(int id)
    {
        return await context.Projects
            .Include(p => p.Events)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task CreateAsync(Project project)
    {
        context.Projects.Add(project);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Project project)
    {
        var existingProject = await context.Projects.FirstOrDefaultAsync(p => p.Id == project.Id);
        if (existingProject == null)
        {
            return;
        }

        existingProject.Name = project.Name;
        existingProject.Description = project.Description;
        existingProject.Color = project.Color;
        existingProject.Status = project.Status;

        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var project = await context.Projects.FindAsync(id);
        if (project != null)
        {
            context.Projects.Remove(project);
            await context.SaveChangesAsync();
        }
    }
}
