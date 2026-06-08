namespace ScheduleApp.Services;

public class AiMonitoringHostedService(
    IServiceProvider serviceProvider,
    ILogger<AiMonitoringHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var aiSettingsService = scope.ServiceProvider.GetRequiredService<IAiSettingsService>();
                var settings = await aiSettingsService.GetAsync(stoppingToken);
                if (!settings.IsEnabled || !settings.EnableProgressMonitoring)
                {
                    continue;
                }

                var monitoringService = scope.ServiceProvider.GetRequiredService<IProjectMonitoringService>();
                await monitoringService.RunAsync(includeAiSummary: false, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI monitoring background cycle failed.");
            }
        }
    }
}
