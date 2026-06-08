using System.Security.Claims;
using System.IO;
using log4net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using ScheduleApp.Data;
using ScheduleApp.Mcp;
using ScheduleApp.Modules.Trading;
using ScheduleApp.Services;

var builder = WebApplication.CreateBuilder(args);
var appDataRoot = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appDataRoot);
var dataProtectionKeysRoot = Path.Combine(appDataRoot, "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeysRoot);
var applicationLogsRoot = Path.Combine(appDataRoot, "Logs");
Directory.CreateDirectory(applicationLogsRoot);
GlobalContext.Properties["AppLogDirectory"] = applicationLogsRoot;
var sqliteConnectionString = ResolveSqliteConnectionString(
    builder.Configuration.GetConnectionString("Default") ?? "Data Source=schedule.db",
    appDataRoot);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson();

builder.Logging.AddLog4Net("log4net.config");
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "ScheduleApp.Antiforgery";
});

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = "AppAuth";
        options.DefaultChallengeScheme = "AppAuth";
    })
    .AddPolicyScheme("AppAuth", "App Authentication", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var hasApiKey = context.Request.Headers.ContainsKey(McpApiKeyAuthenticationHandler.HeaderName)
                || context.Request.Headers.Authorization.Any(value => !string.IsNullOrWhiteSpace(value) && value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase));
            return hasApiKey ? McpApiKeyAuthenticationHandler.SchemeName : CookieAuthenticationDefaults.AuthenticationScheme;
        };
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "ScheduleApp.Auth";
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/mcp"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/mcp"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, McpApiKeyAuthenticationHandler>(McpApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(sqliteConnectionString));
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysRoot))
    .SetApplicationName("ScheduleApp");
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<ProjectManagementMcpTools>();

// Register Services
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IOperationalEventStore, OperationalEventStore>();
builder.Services.AddScoped<IOperationalUsageStore, OperationalUsageStore>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IExportService, ExcelExportService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IAiSettingsService, AiSettingsService>();
builder.Services.AddScoped<IGoogleIntegrationService, GoogleIntegrationService>();
builder.Services.AddScoped<IGoogleOAuthRedirectUriBuilder, GoogleOAuthRedirectUriBuilder>();
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddScoped<IMcpApiKeyService, McpApiKeyService>();
builder.Services.AddScoped<IAiProviderFactory, AiProviderFactory>();
builder.Services.AddScoped<IAiAuditService, AiAuditService>();
builder.Services.AddScoped<IWorkingCalendarService, WorkingCalendarService>();
builder.Services.AddScoped<IUserProactivePreferenceService, UserProactivePreferenceService>();
builder.Services.AddScoped<IRecommendationQueryService, RecommendationQueryService>();
builder.Services.AddScoped<IRecommendationComposer, RecommendationComposer>();
builder.Services.AddScoped<INotificationPlannerService, NotificationPlannerService>();
builder.Services.AddScoped<INotificationCenterService, NotificationCenterService>();
builder.Services.AddScoped<IProjectManagementToolService, ProjectManagementToolService>();
builder.Services.AddScoped<IProjectManagementAgentService, ProjectManagementAgentService>();
builder.Services.AddHttpClient<IWebSearchService, SearxngWebSearchService>();
builder.Services.AddScoped<IZeroWebSearchToolService, ZeroWebSearchToolService>();
builder.Services.AddScoped<IZeroAssistantDataService, ZeroAssistantDataService>();
builder.Services.AddScoped<IZeroLocalToolService, ZeroLocalToolService>();
builder.Services.AddScoped<IZeroAssistantService, ZeroAssistantService>();
builder.Services.AddScoped<IZeroLegacyImportService, ZeroLegacyImportService>();
builder.Services.AddScoped<IProjectMonitoringService, ProjectMonitoringService>();
builder.Services.AddScoped<IDatabaseSchemaUpdater, DatabaseSchemaUpdater>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IProjectStageService, ProjectStageService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IMarketDataSettingsService, MarketDataSettingsService>();
builder.Services.AddScoped<IMarketDataProviderFactory, MarketDataProviderFactory>();
builder.Services.AddScoped<ITradingService, TradingService>();
builder.Services.AddScoped<IMarketPriceService, MarketPriceService>();
builder.Services.AddScoped<ITradingAdvisorService, TradingAdvisorService>();
builder.Services.AddScoped<IOperationalDatabaseSettingsService, OperationalDatabaseSettingsService>();
builder.Services.AddSingleton<IAiConversationStore, AiConversationStore>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddHostedService<AiMonitoringHostedService>();
builder.Services.AddHostedService<NotificationDispatchBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Using SQLite database at {DatabasePath}", ExtractSqliteDataSource(sqliteConnectionString));

        var context = services.GetRequiredService<AppDbContext>();
        var schemaUpdater = services.GetRequiredService<IDatabaseSchemaUpdater>();
        var userAccountService = services.GetRequiredService<IUserAccountService>();
        var zeroLegacyImportService = services.GetRequiredService<IZeroLegacyImportService>();
        var operationalEventStore = services.GetRequiredService<IOperationalEventStore>();
        var operationalUsageStore = services.GetRequiredService<IOperationalUsageStore>();
        await schemaUpdater.EnsureSchemaAsync();
        DbInitializer.Initialize(context);
        await userAccountService.EnsureDefaultAdminAsync();
        await zeroLegacyImportService.ImportIfNeededAsync();
        try
        {
            await operationalEventStore.EnsureReadyAsync();
            await operationalUsageStore.EnsureReadyAsync();
        }
        catch (Exception operationalEx)
        {
            logger.LogWarning(operationalEx, "Operational PostgreSQL Events store is unavailable. Progress will continue with SQLite Events.");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex,
            "Application startup failed while initializing the database at {DatabasePath}. Check IIS write access to the site App_Data folder.",
            ExtractSqliteDataSource(sqliteConnectionString));
        throw;
    }
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

app.MapMcp("/mcp").RequireAuthorization();

app.Run();

static string ResolveSqliteConnectionString(string connectionString, string appDataRoot)
{
    var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(builder.DataSource))
    {
        builder.DataSource = Path.Combine(appDataRoot, "schedule.db");
        return builder.ToString();
    }

    if (Path.IsPathRooted(builder.DataSource))
    {
        return builder.ToString();
    }

    builder.DataSource = Path.Combine(appDataRoot, builder.DataSource);
    return builder.ToString();
}

static string ExtractSqliteDataSource(string connectionString)
{
    var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
    return builder.DataSource;
}
