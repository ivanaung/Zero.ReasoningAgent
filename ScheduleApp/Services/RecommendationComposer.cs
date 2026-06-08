using System.Text;
using Microsoft.Extensions.AI;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IRecommendationComposer
{
    Task<RecommendationResultViewModel> ComposeAsync(RecommendationContextViewModel context, bool useAi, CancellationToken cancellationToken = default);

    Task<string> ComposeNotificationMessageAsync(NotificationScheduleEntry entry, RecommendationContextViewModel context, CancellationToken cancellationToken = default);
}

public class RecommendationComposer(
    IAiSettingsService aiSettingsService,
    IAiProviderFactory aiProviderFactory) : IRecommendationComposer
{
    public async Task<RecommendationResultViewModel> ComposeAsync(RecommendationContextViewModel context, bool useAi, CancellationToken cancellationToken = default)
    {
        var items = context.OverdueTasks
            .Concat(context.BlockedTasks)
            .Concat(context.DueSoonTasks)
            .Concat(context.UpcomingMilestones)
            .Concat(context.UpcomingAssignments)
            .Take(5)
            .ToList();

        var result = new RecommendationResultViewModel
        {
            Horizon = context.Horizon,
            Title = context.Horizon switch
            {
                RecommendationHorizon.NextHour => "Next Hour Focus",
                RecommendationHorizon.Tomorrow => "Tomorrow Preview",
                RecommendationHorizon.PreEventReminder => "Upcoming Reminder",
                RecommendationHorizon.RiskFollowUp => "Risk Follow-up",
                _ => "Recommended Next Actions"
            },
            Summary = BuildFallbackSummary(context, items),
            Items = items,
            UsedAiEnrichment = false
        };

        if (!useAi)
        {
            return result;
        }

        try
        {
            var ai = await aiSettingsService.GetAsync(cancellationToken);
            if (!ai.IsEnabled || !ai.EnableAiEnrichmentForNotifications)
            {
                return result;
            }

            using var client = await aiProviderFactory.CreateChatClientAsync(cancellationToken);
            var prompt = BuildAiPrompt(context, items);
            var response = await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                cancellationToken: cancellationToken);

            if (!string.IsNullOrWhiteSpace(response.Text))
            {
                result.Summary = response.Text.Trim();
                result.UsedAiEnrichment = true;
            }
        }
        catch
        {
            // Fallback stays deterministic.
        }

        return result;
    }

    public async Task<string> ComposeNotificationMessageAsync(NotificationScheduleEntry entry, RecommendationContextViewModel context, CancellationToken cancellationToken = default)
    {
        var result = await ComposeAsync(context, true, cancellationToken);
        return string.IsNullOrWhiteSpace(result.Summary) ? entry.Message : result.Summary;
    }

    private static string BuildFallbackSummary(RecommendationContextViewModel context, IReadOnlyList<RecommendationItemViewModel> items)
    {
        if (items.Count == 0)
        {
            return context.Horizon switch
            {
                RecommendationHorizon.NextHour => "No urgent work is scheduled in the next hour.",
                RecommendationHorizon.Tomorrow => "Tomorrow looks clear based on current assignments.",
                _ => "No urgent follow-up is needed right now."
            };
        }

        var top = items[0];
        return context.Horizon switch
        {
            RecommendationHorizon.NextHour => $"Next hour focus: {top.Title}. Recommended action: start with the highest-risk open item before {FormatDue(top)}.",
            RecommendationHorizon.Tomorrow => $"Tomorrow: {items.Count} important item(s) need attention. Recommended first action: prepare {top.Title}.",
            RecommendationHorizon.PreEventReminder => $"{top.Title} starts soon. Recommended action: open the task and prepare any blocking material now.",
            RecommendationHorizon.RiskFollowUp => $"Attention needed: {top.Title} is driving current delivery risk. Recommended action: review the blocker and unblock the next dependency.",
            _ => $"Recommended action now: {top.Title}. Review this first because it has the strongest time or risk signal."
        };
    }

    private static string BuildAiPrompt(RecommendationContextViewModel context, IReadOnlyList<RecommendationItemViewModel> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Create one short operational recommendation.");
        builder.AppendLine($"Horizon: {context.Horizon}");
        builder.AppendLine(context.CalendarSummary);
        foreach (var item in items.Take(5))
        {
            builder.AppendLine($"- {item.Title} | {item.Detail} | status={item.Status} | priority={item.Priority}");
        }

        builder.AppendLine("Rules: max 2 sentences, concrete next action, no invented facts.");
        return builder.ToString();
    }

    private static string FormatDue(RecommendationItemViewModel item)
        => item.EndUtc.HasValue ? item.EndUtc.Value.ToLocalTime().ToString("HH:mm") : "the scheduled time";
}
