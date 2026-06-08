using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IProjectStageService
{
    Task<List<ProjectStage>> GetAllAsync();
    Task<ProjectStage?> GetByIdAsync(int id);
    Task<ProjectStage> EnsureStageAsync(string stageName);
    Task CreateAsync(ProjectStage stage);
    Task UpdateAsync(ProjectStage stage);
    Task DeleteAsync(int id);
}

public class ProjectStageService(AppDbContext context) : IProjectStageService
{
    public async Task<List<ProjectStage>> GetAllAsync()
    {
        return await context.ProjectStages
            .OrderBy(stage => stage.Name)
            .ToListAsync();
    }

    public async Task<ProjectStage?> GetByIdAsync(int id)
    {
        return await context.ProjectStages
            .FirstOrDefaultAsync(stage => stage.Id == id);
    }

    public async Task<ProjectStage> EnsureStageAsync(string stageName)
    {
        var normalizedName = stageName.Trim();
        var existing = await context.ProjectStages
            .FirstOrDefaultAsync(stage => stage.Name == normalizedName);

        if (existing != null)
        {
            return existing;
        }

        var stage = new ProjectStage
        {
            Name = normalizedName
        };

        context.ProjectStages.Add(stage);
        await context.SaveChangesAsync();
        return stage;
    }

    public async Task CreateAsync(ProjectStage stage)
    {
        stage.Name = stage.Name.Trim();
        stage.Description = string.IsNullOrWhiteSpace(stage.Description) ? null : stage.Description.Trim();

        context.ProjectStages.Add(stage);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ProjectStage stage)
    {
        var existingStage = await context.ProjectStages
            .Include(item => item.Events)
            .FirstOrDefaultAsync(item => item.Id == stage.Id);

        if (existingStage == null)
        {
            return;
        }

        var previousName = existingStage.Name;

        existingStage.Name = stage.Name.Trim();
        existingStage.Description = string.IsNullOrWhiteSpace(stage.Description) ? null : stage.Description.Trim();

        foreach (var evt in existingStage.Events)
        {
            if (!string.Equals(previousName, existingStage.Name, StringComparison.Ordinal))
            {
                evt.AreaName = existingStage.Name;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var stage = await context.ProjectStages
            .Include(item => item.Events)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (stage == null)
        {
            return;
        }

        foreach (var evt in stage.Events)
        {
            evt.StageId = null;
            evt.AreaName = stage.Name;
        }

        context.ProjectStages.Remove(stage);
        await context.SaveChangesAsync();
    }
}
