using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class SettingsController(
    ISettingsService settingsService,
    IAiSettingsService aiSettingsService,
    IAiProviderFactory aiProviderFactory,
    IGoogleIntegrationService googleIntegrationService,
    IGoogleOAuthRedirectUriBuilder googleOAuthRedirectUriBuilder,
    IZeroAssistantDataService zeroAssistantDataService,
    IZeroAssistantService zeroAssistantService,
    IMcpApiKeyService mcpApiKeyService,
    IOperationalDatabaseSettingsService operationalDatabaseSettingsService,
    IMarketDataSettingsService marketDataSettingsService,
    IOperationalEventStore operationalEventStore,
    IOperationalUsageStore operationalUsageStore,
    ILogger<SettingsController> logger) : Controller
{
    public async Task<IActionResult> Index(string? tab = null, bool saved = false, bool aiSaved = false, bool integrationSaved = false, bool mcpSaved = false, bool zeroSaved = false, bool databaseSaved = false, bool marketDataSaved = false)
    {
        var model = new SettingsPageViewModel
        {
            Preferences = await settingsService.GetSettingsAsync(),
            Ai = await aiSettingsService.GetInputModelAsync(),
            Integration = await googleIntegrationService.GetInputModelAsync(),
            Mcp = await mcpApiKeyService.GetSettingsViewModelAsync(TempData["McpGeneratedApiKey"] as string),
            Zero = await BuildZeroSettingsInputAsync(),
            Database = await operationalDatabaseSettingsService.GetInputAsync(),
            MarketData = await marketDataSettingsService.GetInputAsync(),
            Saved = saved,
            AiSaved = aiSaved,
            IntegrationSaved = integrationSaved,
            McpSaved = mcpSaved,
            ZeroSaved = zeroSaved,
            DatabaseSaved = databaseSaved,
            MarketDataSaved = marketDataSaved,
            ActiveTab = NormalizeSettingsTab(tab),
            AvailableTools = BuildAvailableTools()
        };
        model.Integration.RedirectUri = await googleOAuthRedirectUriBuilder.BuildCallbackUriAsync(Request);

        if (model.ActiveTab == "ai")
        {
            var health = await aiProviderFactory.GetHealthAsync();
            model.AiHealthStatus = health.Healthy ? "Online" : "Offline";
            model.AiHealthMessage = health.Message;
        }
        else
        {
            model.AiHealthStatus = "Not checked";
            model.AiHealthMessage = "Open AI settings to check provider health.";
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePreferences([Bind(Prefix = "Preferences")] UserSettings settings)
    {
        if (ModelState.IsValid)
        {
            await settingsService.SaveSettingsAsync(settings);
            return RedirectToAction(nameof(Index), new { saved = true, tab = "display" });
        }

        return View("Index", new SettingsPageViewModel
        {
            Preferences = settings,
            Ai = await aiSettingsService.GetInputModelAsync(),
            Integration = WithGoogleRedirectUri(await googleIntegrationService.GetInputModelAsync()),
            Mcp = await mcpApiKeyService.GetSettingsViewModelAsync(),
            Zero = await BuildZeroSettingsInputAsync(),
            Database = await operationalDatabaseSettingsService.GetInputAsync(),
            MarketData = await marketDataSettingsService.GetInputAsync(),
            ActiveTab = "display",
            AvailableTools = BuildAvailableTools()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAi([Bind(Prefix = "Ai")] AiSettingsInputViewModel model)
    {
        if (ModelState.IsValid)
        {
            await aiSettingsService.SaveAsync(model);
            return RedirectToAction(nameof(Index), new { aiSaved = true, tab = "ai" });
        }

        var health = await aiProviderFactory.TestAsync(model);
        return View("Index", new SettingsPageViewModel
        {
            Preferences = await settingsService.GetSettingsAsync(),
            Ai = model,
            Integration = WithGoogleRedirectUri(await googleIntegrationService.GetInputModelAsync()),
            Mcp = await mcpApiKeyService.GetSettingsViewModelAsync(),
            Zero = await BuildZeroSettingsInputAsync(),
            Database = await operationalDatabaseSettingsService.GetInputAsync(),
            MarketData = await marketDataSettingsService.GetInputAsync(),
            ActiveTab = "ai",
            AiHealthStatus = health.Healthy ? "Online" : "Offline",
            AiHealthMessage = health.Message,
            AvailableTools = BuildAvailableTools()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIntegration([Bind(Prefix = "Integration")] GoogleIntegrationSettingsInputViewModel model)
    {
        if (ModelState.IsValid)
        {
            await googleIntegrationService.SaveAsync(model);
            return RedirectToAction(nameof(Index), new { tab = "integration", integrationSaved = true });
        }

        return View("Index", new SettingsPageViewModel
        {
            Preferences = await settingsService.GetSettingsAsync(),
            Ai = await aiSettingsService.GetInputModelAsync(),
            Integration = WithGoogleRedirectUri(model),
            Mcp = await mcpApiKeyService.GetSettingsViewModelAsync(),
            Zero = await BuildZeroSettingsInputAsync(),
            Database = await operationalDatabaseSettingsService.GetInputAsync(),
            MarketData = await marketDataSettingsService.GetInputAsync(),
            ActiveTab = "integration",
            AiHealthStatus = "Not checked",
            AiHealthMessage = "Open AI settings to check provider health.",
            AvailableTools = BuildAvailableTools()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateMcpApiKey([Bind(Prefix = "McpGenerate")] GenerateMcpApiKeyInputViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var health = await aiProviderFactory.GetHealthAsync();
            return View("Index", new SettingsPageViewModel
            {
            Preferences = await settingsService.GetSettingsAsync(),
                Ai = await aiSettingsService.GetInputModelAsync(),
                Integration = WithGoogleRedirectUri(await googleIntegrationService.GetInputModelAsync()),
                Mcp = await mcpApiKeyService.GetSettingsViewModelAsync(),
                Zero = await BuildZeroSettingsInputAsync(),
                Database = await operationalDatabaseSettingsService.GetInputAsync(),
                MarketData = await marketDataSettingsService.GetInputAsync(),
                ActiveTab = "ai",
                AiHealthStatus = health.Healthy ? "Online" : "Offline",
                AiHealthMessage = health.Message,
                AvailableTools = BuildAvailableTools()
            });
        }

        TempData["McpGeneratedApiKey"] = await mcpApiKeyService.GenerateForCurrentUserAsync(model.Name, cancellationToken);
        return RedirectToAction(nameof(Index), new { tab = "ai", mcpSaved = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveZero([Bind(Prefix = "Zero")] ZeroAssistantSettingsInputViewModel model, CancellationToken cancellationToken = default)
    {
        if (ModelState.IsValid)
        {
            await zeroAssistantDataService.SaveSettingsAsync(model, cancellationToken);
            return RedirectToAction(nameof(Index), new { tab = "ai", zeroSaved = true });
        }

        var health = await aiProviderFactory.GetHealthAsync(cancellationToken);
        return View("Index", new SettingsPageViewModel
        {
            Preferences = await settingsService.GetSettingsAsync(),
            Ai = await aiSettingsService.GetInputModelAsync(),
            Integration = WithGoogleRedirectUri(await googleIntegrationService.GetInputModelAsync()),
            Mcp = await mcpApiKeyService.GetSettingsViewModelAsync(),
            Zero = await BuildZeroSettingsInputAsync(model, cancellationToken),
            Database = await operationalDatabaseSettingsService.GetInputAsync(cancellationToken),
            MarketData = await marketDataSettingsService.GetInputAsync(cancellationToken),
            ActiveTab = "ai",
            AiHealthStatus = health.Healthy ? "Online" : "Offline",
            AiHealthMessage = health.Message,
            AvailableTools = BuildAvailableTools()
        });
    }

    [HttpGet("settings/market-data")]
    public async Task<IActionResult> MarketData(bool marketDataSaved = false, CancellationToken cancellationToken = default)
    {
        return View("Index", await BuildSettingsPageForMarketDataAsync(
            await marketDataSettingsService.GetInputAsync(cancellationToken),
            null,
            marketDataSaved,
            cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMarketData([Bind(Prefix = "MarketData")] MarketDataSettingsInputViewModel model, CancellationToken cancellationToken = default)
    {
        await marketDataSettingsService.SaveAsync(model, cancellationToken);
        return RedirectToAction(nameof(MarketData), new { marketDataSaved = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestMarketData([Bind(Prefix = "MarketData")] MarketDataSettingsInputViewModel model, MarketDataProviderName providerName, CancellationToken cancellationToken = default)
    {
        await marketDataSettingsService.SaveAsync(model, cancellationToken);
        var result = await marketDataSettingsService.TestAsync(providerName, cancellationToken);
        return View("Index", await BuildSettingsPageForMarketDataAsync(
            await marketDataSettingsService.GetInputAsync(cancellationToken),
            result,
            false,
            cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDatabase([Bind(Prefix = "Database")] OperationalDatabaseInputViewModel model, CancellationToken cancellationToken = default)
    {
        if (ModelState.IsValid)
        {
            await operationalDatabaseSettingsService.SaveAsync(model, cancellationToken);
            if (model.IsEnabled)
            {
                try
                {
                    await operationalEventStore.EnsureReadyAsync(cancellationToken);
                    await operationalUsageStore.EnsureReadyAsync(cancellationToken);
                    TempData["DatabaseOperationalSync"] = "PostgreSQL operational tables are ready and existing Events, chat history, audit history, and finance transactions were synchronized.";
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "PostgreSQL Events table initialization failed after saving database settings.");
                    TempData["DatabaseOperationalSyncError"] = "PostgreSQL settings were saved, but Events table initialization failed. Progress will keep using SQLite until PostgreSQL is reachable.";
                }
            }

            return RedirectToAction(nameof(Index), new { tab = "database", databaseSaved = true });
        }

        return View("Index", await BuildSettingsPageForDatabaseAsync(model, null, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestDatabase([Bind(Prefix = "Database")] OperationalDatabaseInputViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildSettingsPageForDatabaseAsync(model, null, cancellationToken));
        }

        var result = await operationalDatabaseSettingsService.TestAsync(model, cancellationToken);
        return View("Index", await BuildSettingsPageForDatabaseAsync(model, result, cancellationToken));
    }

    private GoogleIntegrationSettingsInputViewModel WithGoogleRedirectUri(GoogleIntegrationSettingsInputViewModel model)
    {
        model.RedirectUri = googleOAuthRedirectUriBuilder.BuildCallbackUriAsync(Request).GetAwaiter().GetResult();
        return model;
    }

    private async Task<ZeroAssistantSettingsInputViewModel> BuildZeroSettingsInputAsync(ZeroAssistantSettingsInputViewModel? model = null, CancellationToken cancellationToken = default)
    {
        model ??= await zeroAssistantDataService.GetSettingsInputAsync(cancellationToken);
        model.PiperVoices = await zeroAssistantService.GetPiperVoicesAsync(cancellationToken);
        return model;
    }

    private async Task<SettingsPageViewModel> BuildSettingsPageForDatabaseAsync(
        OperationalDatabaseInputViewModel database,
        OperationalDatabaseTestResultViewModel? testResult,
        CancellationToken cancellationToken)
    {
        return new SettingsPageViewModel
        {
            Preferences = await settingsService.GetSettingsAsync(),
            Ai = await aiSettingsService.GetInputModelAsync(cancellationToken),
            Integration = WithGoogleRedirectUri(await googleIntegrationService.GetInputModelAsync(cancellationToken)),
            Mcp = await mcpApiKeyService.GetSettingsViewModelAsync(),
            Zero = await BuildZeroSettingsInputAsync(cancellationToken: cancellationToken),
            Database = database,
            MarketData = await marketDataSettingsService.GetInputAsync(cancellationToken),
            DatabaseTestResult = testResult,
            ActiveTab = "database",
            AiHealthStatus = "Not checked",
            AiHealthMessage = "Open AI settings to check provider health.",
            AvailableTools = BuildAvailableTools()
        };
    }

    private async Task<SettingsPageViewModel> BuildSettingsPageForMarketDataAsync(
        MarketDataSettingsInputViewModel marketData,
        MarketDataProviderTestResultViewModel? testResult,
        bool marketDataSaved,
        CancellationToken cancellationToken)
    {
        return new SettingsPageViewModel
        {
            Preferences = await settingsService.GetSettingsAsync(),
            Ai = await aiSettingsService.GetInputModelAsync(cancellationToken),
            Integration = WithGoogleRedirectUri(await googleIntegrationService.GetInputModelAsync(cancellationToken)),
            Mcp = await mcpApiKeyService.GetSettingsViewModelAsync(),
            Zero = await BuildZeroSettingsInputAsync(cancellationToken: cancellationToken),
            Database = await operationalDatabaseSettingsService.GetInputAsync(cancellationToken),
            MarketData = marketData,
            MarketDataTestResult = testResult,
            MarketDataSaved = marketDataSaved,
            ActiveTab = "market-data",
            AiHealthStatus = "Not checked",
            AiHealthMessage = "Open AI settings to check provider health.",
            AvailableTools = BuildAvailableTools()
        };
    }

    private static string NormalizeSettingsTab(string? tab)
    {
        if (string.Equals(tab, "ai", StringComparison.OrdinalIgnoreCase))
        {
            return "ai";
        }

        if (string.Equals(tab, "integration", StringComparison.OrdinalIgnoreCase))
        {
            return "integration";
        }

        if (string.Equals(tab, "market-data", StringComparison.OrdinalIgnoreCase))
        {
            return "market-data";
        }

        return string.Equals(tab, "database", StringComparison.OrdinalIgnoreCase)
            ? "database"
            : "display";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeMcpApiKey(int id, CancellationToken cancellationToken = default)
    {
        await mcpApiKeyService.RevokeForCurrentUserAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index), new { tab = "ai", mcpSaved = true });
    }

    [HttpGet]
    public async Task<IActionResult> AiModels(AiProviderType providerType, string endpointUrl, string? modelId = null, string? apiKey = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await aiProviderFactory.GetAvailableModelsAsync(new AiSettingsInputViewModel
            {
                IsEnabled = true,
                ProviderType = providerType,
                EndpointUrl = endpointUrl,
                ModelId = modelId ?? string.Empty,
                ApiKey = apiKey,
                SystemPrompt = "test",
                DefaultTimeZone = "Pacific/Auckland",
                WorkingHoursStart = "08:00",
                WorkingHoursEnd = "17:00",
                WorkingDays = "Mon,Tue,Wed,Thu,Fri"
            }, cancellationToken);

            return Json(new { success = true, models });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message, models = Array.Empty<string>() });
        }
    }

    [HttpGet]
    public async Task<IActionResult> TestAi(AiProviderType providerType, string endpointUrl, string modelId, string? apiKey = null, CancellationToken cancellationToken = default)
    {
        var result = await aiProviderFactory.TestAsync(new AiSettingsInputViewModel
        {
            IsEnabled = true,
            ProviderType = providerType,
            EndpointUrl = endpointUrl,
            ModelId = modelId,
            ApiKey = apiKey,
            SystemPrompt = "You are a connectivity test agent. Reply with OK.",
            DefaultTimeZone = "Pacific/Auckland",
            WorkingHoursStart = "08:00",
            WorkingHoursEnd = "17:00",
            WorkingDays = "Mon,Tue,Wed,Thu,Fri"
        }, cancellationToken);

        return Json(result);
    }

    private static IReadOnlyList<AiToolDebugItemViewModel> BuildAvailableTools()
    {
        return
        [
            new() { Name = "GetProjectSummary", Signature = "GetProjectSummary(projectId)", Description = "Returns the current task/status summary for one project." },
            new() { Name = "GetTaskSummary", Signature = "GetTaskSummary(taskId)", Description = "Returns full detail for a single task." },
            new() { Name = "GetTasksByProject", Signature = "GetTasksByProject(projectId)", Description = "Lists tasks for a project." },
            new() { Name = "GetOverdueTasks", Signature = "GetOverdueTasks(projectId?)", Description = "Lists overdue tasks globally or per project." },
            new() { Name = "GetBlockedTasks", Signature = "GetBlockedTasks(projectId?)", Description = "Lists blocked tasks globally or per project." },
            new() { Name = "GetUpcomingMilestones", Signature = "GetUpcomingMilestones(projectId?)", Description = "Lists upcoming critical milestones." },
            new() { Name = "SearchProjects", Signature = "SearchProjects(query)", Description = "Finds projects by name or description." },
            new() { Name = "SearchTasks", Signature = "SearchTasks(query)", Description = "Finds tasks by title or description." },
            new() { Name = "FindTaskByName", Signature = "FindTaskByName(name)", Description = "Finds the single best task match for a task name or short title." },
            new() { Name = "CreateProject", Signature = "CreateProject(name, description?, color?)", Description = "Creates a new top-level project." },
            new() { Name = "CreateTask", Signature = "CreateTask(title, description?, projectId?, projectName?, dependsOnTaskId?, categoryId?, stageId?, stageName?, assignee?, startHint?, endHint?, allDay?)", Description = "Creates a task through the scheduling rules and existing task service." },
            new() { Name = "UpdateTaskDates", Signature = "UpdateTaskDates(taskId, startDateTime?, endDateTime?, reason?)", Description = "Reschedules an existing task, respecting approval rules." },
            new() { Name = "AssignTask", Signature = "AssignTask(taskId, assignee)", Description = "Assigns a task to a user label." },
            new() { Name = "AddTaskComment", Signature = "AddTaskComment(taskId, comment)", Description = "Adds a comment to a task." },
            new() { Name = "AddProjectComment", Signature = "AddProjectComment(projectId, comment)", Description = "Adds a comment to a project." },
            new() { Name = "GetTeamAvailability", Signature = "GetTeamAvailability(userId?, from?, to?)", Description = "Shows scheduled work for a user or the team over a date range." },
            new() { Name = "GetUserAssignedTasks", Signature = "GetUserAssignedTasks(userId, from?, to?)", Description = "Lists assigned tasks for one user in a time window." },
            new() { Name = "GetDueSoonTasks", Signature = "GetDueSoonTasks(userId, from?, to?)", Description = "Lists tasks due soon for one user." },
            new() { Name = "GetProjectRisks", Signature = "GetProjectRisks(userId?, projectId?)", Description = "Lists blocked or overdue risk items." },
            new() { Name = "GetCalendarContext", Signature = "GetCalendarContext(userId, from?, to?)", Description = "Returns assignment context for a user calendar window." },
            new() { Name = "GetGoogleIntegrationStatus", Signature = "GetGoogleIntegrationStatus()", Description = "Returns whether Google email/calendar integration is configured and connected." },
            new() { Name = "GetInboxSummary", Signature = "GetInboxSummary(maxItems?)", Description = "Returns recent Gmail inbox items from the linked Google account." },
            new() { Name = "SearchInboxEmails", Signature = "SearchInboxEmails(query, maxItems?)", Description = "Searches linked Gmail inbox messages by keyword." },
            new() { Name = "GetLinkedCalendarEvents", Signature = "GetLinkedCalendarEvents(from?, to?, maxItems?)", Description = "Returns upcoming events from the linked Google Calendar account." },
            new() { Name = "CalculateNextWorkingSlot", Signature = "CalculateNextWorkingSlot(after?, durationMinutes, preferredDate?, preferredTime?)", Description = "Calculates the next valid slot using working calendar rules." },
            new() { Name = "GetDependencyChain", Signature = "GetDependencyChain(taskId)", Description = "Returns the dependency chain for a task." },
            new() { Name = "GetCurrentDateTime", Signature = "GetCurrentDateTime()", Description = "Returns the current server date/time." },
            new() { Name = "GetWorkingCalendarRules", Signature = "GetWorkingCalendarRules()", Description = "Returns timezone, hours, and working-day rules." },
            new() { Name = "CreateFollowUpTask", Signature = "CreateFollowUpTask(taskId, title, assignee?)", Description = "Creates a follow-up task from an existing task." },
            new() { Name = "CreateNotificationScheduleEntry", Signature = "CreateNotificationScheduleEntry(userId, notificationType, scheduledForUtc, message?, taskId?, projectId?)", Description = "Creates a manual notification queue entry." },
            new() { Name = "CancelNotificationScheduleEntriesForTask", Signature = "CancelNotificationScheduleEntriesForTask(taskId)", Description = "Cancels pending notifications for a task." },
            new() { Name = "RecomputeNotificationPlanForTask", Signature = "RecomputeNotificationPlanForTask(taskId)", Description = "Rebuilds pending notifications for a task." },
            new() { Name = "GetNotificationContext", Signature = "GetNotificationContext(notificationId)", Description = "Returns context for a notification schedule entry." },
            new() { Name = "ConvertTime", Signature = "ConvertTime(targetTimeZone, timeToConvert?)", Description = "Converts and formats current time to a global timezone location." },
            new() { Name = "GetFinanceSummary", Signature = "GetFinanceSummary(scope?, month?)", Description = "Returns monthly finance summary for all, personal, or business scope." },
            new() { Name = "GetUpcomingBills", Signature = "GetUpcomingBills(days?)", Description = "Returns upcoming recurring expense items due soon." },
            new() { Name = "GetProjectFinanceSummary", Signature = "GetProjectFinanceSummary(projectId)", Description = "Returns finance totals for one linked project." },
            new() { Name = "CreateFinanceTransaction", Signature = "CreateFinanceTransaction(title, amount, scope?, type?, categoryName?, projectId?, transactionDate?)", Description = "Creates a finance transaction entry for manual tracking." },
            new() { Name = "SearchAsync", Signature = "SearchAsync(query, maxResults?)", Description = "Searches the public web through local SearXNG for current information, public documentation, versions, and websites." }
        ];
    }
}
