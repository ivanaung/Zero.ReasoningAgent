using Microsoft.Extensions.AI;
using OllamaSharp;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ScheduleApp.Services;

public interface IAiProviderFactory
{
    Task<IChatClient> CreateChatClientAsync(CancellationToken cancellationToken = default);

    Task<AiHealthViewModel> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<AiHealthViewModel> TestAsync(AiSettingsInputViewModel model, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAvailableModelsAsync(AiSettingsInputViewModel model, CancellationToken cancellationToken = default);
}

public class AiProviderConfigurationException(string message) : InvalidOperationException(message);

public class AiProviderFactory(
    IAiSettingsService aiSettingsService,
    IHttpClientFactory httpClientFactory) : IAiProviderFactory
{
    public async Task<IChatClient> CreateChatClientAsync(CancellationToken cancellationToken = default)
    {
        var settings = await aiSettingsService.GetAsync(cancellationToken);
        return CreateChatClient(settings);
    }

    public async Task<AiHealthViewModel> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var settings = await aiSettingsService.GetAsync(cancellationToken);
        return await TestAsync(MapToInputModel(settings), cancellationToken);
    }

    public async Task<AiHealthViewModel> TestAsync(AiSettingsInputViewModel model, CancellationToken cancellationToken = default)
    {
        var settings = MapToEntity(model);
        var result = new AiHealthViewModel
        {
            Enabled = settings.IsEnabled,
            Provider = settings.ProviderType.ToString(),
            ModelId = settings.ModelId
        };

        if (!settings.IsEnabled)
        {
            result.Message = "AI assistant is disabled in settings.";
            return result;
        }

        try
        {
            using var client = CreateChatClient(settings);
            var response = await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Reply with the single word OK.")],
                cancellationToken: cancellationToken);

            result.Healthy = true;
            result.Message = string.IsNullOrWhiteSpace(response.Text)
                ? "Provider responded successfully."
                : response.Text.Trim();
        }
        catch (Exception ex)
        {
            result.Healthy = false;
            result.Message = ex.Message;
        }

        return result;
    }

    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(AiSettingsInputViewModel model, CancellationToken cancellationToken = default)
    {
        var settings = MapToEntity(model);
        Validate(settings);

        if (settings.ProviderType != AiProviderType.Ollama)
        {
            return string.IsNullOrWhiteSpace(settings.ModelId) ? [] : [settings.ModelId];
        }

        var endpoint = NormalizeEndpoint(settings.EndpointUrl);
        using var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync($"{endpoint}/api/tags", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: cancellationToken);
        var names = payload?.Models?
            .Select(modelItem => modelItem.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList() ?? [];

        if (names.Count == 0 && !string.IsNullOrWhiteSpace(settings.ModelId))
        {
            names.Add(settings.ModelId);
        }

        return names;
    }

    private IChatClient CreateChatClient(AiSettings settings)
    {
        Validate(settings);

        IChatClient baseClient = settings.ProviderType switch
        {
            AiProviderType.Ollama => CreateOllamaClient(settings),
            AiProviderType.OpenAI => throw new AiProviderConfigurationException("OpenAI support is not yet enabled in this deployment. Add an OpenAI-backed IChatClient in AiProviderFactory."),
            AiProviderType.AzureOpenAI => throw new AiProviderConfigurationException("Azure OpenAI support is not yet enabled in this deployment. Add an Azure-backed IChatClient in AiProviderFactory."),
            AiProviderType.Anthropic => throw new AiProviderConfigurationException("Anthropic support is not yet enabled in this deployment. Add an Anthropic-backed IChatClient in AiProviderFactory."),
            _ => throw new AiProviderConfigurationException("Unsupported AI provider.")
        };

        var client = baseClient.AsBuilder()
            .ConfigureOptions(options =>
            {
                options.ModelId ??= settings.ModelId;
                options.Temperature = (float?)settings.Temperature;
                options.MaxOutputTokens = settings.MaxTokens;
            })
            .Build();

        return client;
    }

    private static IChatClient CreateOllamaClient(AiSettings settings)
    {
        var endpoint = new Uri(NormalizeEndpoint(settings.EndpointUrl));
        return new OllamaApiClient(endpoint, settings.ModelId);
    }

    private static string NormalizeEndpoint(string? endpointUrl)
    {
        return string.IsNullOrWhiteSpace(endpointUrl)
            ? "http://localhost:11434"
            : endpointUrl.Trim().TrimEnd('/');
    }

    private static AiSettings MapToEntity(AiSettingsInputViewModel model)
    {
        return new AiSettings
        {
            IsEnabled = model.IsEnabled,
            ProviderType = model.ProviderType,
            ModelId = model.ModelId?.Trim() ?? string.Empty,
            EndpointUrl = model.EndpointUrl?.Trim() ?? string.Empty,
            ApiKey = string.Equals(model.ApiKey, "configured", StringComparison.Ordinal) ? null : model.ApiKey?.Trim(),
            Temperature = model.Temperature,
            MaxTokens = model.MaxTokens,
            SystemPrompt = string.IsNullOrWhiteSpace(model.SystemPrompt) ? string.Empty : model.SystemPrompt.Trim(),
            EnableToolCalling = model.EnableToolCalling,
            EnableStreaming = model.EnableStreaming,
            EnableProgressMonitoring = model.EnableProgressMonitoring,
            DefaultTimeZone = model.DefaultTimeZone?.Trim() ?? string.Empty,
            WorkingHoursStart = model.WorkingHoursStart,
            WorkingHoursEnd = model.WorkingHoursEnd,
            WorkingDays = model.WorkingDays?.Trim() ?? string.Empty,
            AutoCreateLowRiskTasks = model.AutoCreateLowRiskTasks,
            RequireApprovalForScheduleChange = model.RequireApprovalForScheduleChange,
            RequireApprovalForTaskDeletion = model.RequireApprovalForTaskDeletion,
            EnableProactiveAssist = model.EnableProactiveAssist,
            EnableNextHourRecommendations = model.EnableNextHourRecommendations,
            EnableTomorrowRecommendations = model.EnableTomorrowRecommendations,
            EnablePreEventReminders = model.EnablePreEventReminders,
            PreEventReminderMinutes = model.PreEventReminderMinutes,
            MorningDigestTime = model.MorningDigestTime,
            AfternoonDigestTime = model.AfternoonDigestTime,
            MaxRecommendationsPerNotification = model.MaxRecommendationsPerNotification,
            QuietHoursStart = model.QuietHoursStart,
            QuietHoursEnd = model.QuietHoursEnd,
            SendInAppNotifications = model.SendInAppNotifications,
            SendPushNotifications = model.SendPushNotifications,
            SendEmailNotifications = model.SendEmailNotifications,
            EnableAiEnrichmentForNotifications = model.EnableAiEnrichmentForNotifications,
            NotificationLookaheadHours = model.NotificationLookaheadHours,
            DigestLookaheadDays = model.DigestLookaheadDays,
            RecomputeOnTaskUpdate = model.RecomputeOnTaskUpdate,
            RecomputeOnAssignmentChange = model.RecomputeOnAssignmentChange,
            RecomputeOnDependencyChange = model.RecomputeOnDependencyChange,
            RequireUserOptInForProactiveAssist = model.RequireUserOptInForProactiveAssist,
            DefaultUserTimeZone = model.DefaultUserTimeZone?.Trim() ?? string.Empty
        };
    }

    private static AiSettingsInputViewModel MapToInputModel(AiSettings entity)
    {
        return new AiSettingsInputViewModel
        {
            IsEnabled = entity.IsEnabled,
            ProviderType = entity.ProviderType,
            ModelId = entity.ModelId,
            EndpointUrl = entity.EndpointUrl,
            ApiKey = string.IsNullOrWhiteSpace(entity.ApiKey) ? null : "configured",
            Temperature = entity.Temperature,
            MaxTokens = entity.MaxTokens,
            SystemPrompt = entity.SystemPrompt,
            EnableToolCalling = entity.EnableToolCalling,
            EnableStreaming = entity.EnableStreaming,
            EnableProgressMonitoring = entity.EnableProgressMonitoring,
            DefaultTimeZone = entity.DefaultTimeZone,
            WorkingHoursStart = entity.WorkingHoursStart,
            WorkingHoursEnd = entity.WorkingHoursEnd,
            WorkingDays = entity.WorkingDays,
            AutoCreateLowRiskTasks = entity.AutoCreateLowRiskTasks,
            RequireApprovalForScheduleChange = entity.RequireApprovalForScheduleChange,
            RequireApprovalForTaskDeletion = entity.RequireApprovalForTaskDeletion,
            EnableProactiveAssist = entity.EnableProactiveAssist,
            EnableNextHourRecommendations = entity.EnableNextHourRecommendations,
            EnableTomorrowRecommendations = entity.EnableTomorrowRecommendations,
            EnablePreEventReminders = entity.EnablePreEventReminders,
            PreEventReminderMinutes = entity.PreEventReminderMinutes,
            MorningDigestTime = entity.MorningDigestTime,
            AfternoonDigestTime = entity.AfternoonDigestTime,
            MaxRecommendationsPerNotification = entity.MaxRecommendationsPerNotification,
            QuietHoursStart = entity.QuietHoursStart,
            QuietHoursEnd = entity.QuietHoursEnd,
            SendInAppNotifications = entity.SendInAppNotifications,
            SendPushNotifications = entity.SendPushNotifications,
            SendEmailNotifications = entity.SendEmailNotifications,
            EnableAiEnrichmentForNotifications = entity.EnableAiEnrichmentForNotifications,
            NotificationLookaheadHours = entity.NotificationLookaheadHours,
            DigestLookaheadDays = entity.DigestLookaheadDays,
            RecomputeOnTaskUpdate = entity.RecomputeOnTaskUpdate,
            RecomputeOnAssignmentChange = entity.RecomputeOnAssignmentChange,
            RecomputeOnDependencyChange = entity.RecomputeOnDependencyChange,
            RequireUserOptInForProactiveAssist = entity.RequireUserOptInForProactiveAssist,
            DefaultUserTimeZone = entity.DefaultUserTimeZone
        };
    }

    private static void Validate(AiSettings settings)
    {
        if (!settings.IsEnabled)
        {
            throw new AiProviderConfigurationException("AI assistant is disabled.");
        }

        if (string.IsNullOrWhiteSpace(settings.ModelId))
        {
            throw new AiProviderConfigurationException("AI model is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.EndpointUrl))
        {
            throw new AiProviderConfigurationException("AI endpoint is not configured.");
        }
    }

    private sealed class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelItem>? Models { get; set; }
    }

    private sealed class OllamaModelItem
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
