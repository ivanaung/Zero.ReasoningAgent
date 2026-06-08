using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ScheduleApp.Models.ViewModels;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScheduleApp.Services;

public interface IProjectManagementAgentService
{
    Task<AiChatResponse> SendAsync(AiChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAsync(AiChatRequest request, CancellationToken cancellationToken = default);

    void ClearConversation(string conversationId);
}

public class ProjectManagementAgentService(
    IAiProviderFactory aiProviderFactory,
    IAiSettingsService aiSettingsService,
    IProjectManagementToolService toolService,
    IZeroWebSearchToolService webSearchToolService,
    IAiConversationStore conversationStore,
    IAiAuditService auditService,
    ICurrentUserService currentUserService) : IProjectManagementAgentService
{
    public async Task<AiChatResponse> SendAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await aiSettingsService.GetAsync(cancellationToken);
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("n")
            : request.ConversationId!;
        var conversationState = conversationStore.GetOrCreate(conversationId);
        var routingMessage = ExtractCurrentUserRequest(request.Message);

        if (ShouldRouteDirectTaskLookup(routingMessage))
        {
            var directLookupResponse = await TryAnswerTaskLookupDirectlyAsync(routingMessage, conversationId, settings, cancellationToken);
            if (directLookupResponse != null)
            {
                return directLookupResponse;
            }
        }

        if (ShouldRouteDirectTaskCreation(routingMessage))
        {
            var directResponse = await TryCreateTaskDirectlyAsync(routingMessage, conversationId, settings, cancellationToken);
            if (directResponse != null)
            {
                return directResponse;
            }
        }

        using var chatClient = await aiProviderFactory.CreateChatClientAsync(cancellationToken);
        var agent = CreateAgent(chatClient, settings);

        var effectiveMessage = BuildEffectiveMessage(request.Message, conversationState, settings);
        var session = await GetOrCreateSessionAsync(agent, conversationId, cancellationToken);
        var response = await agent.RunAsync(effectiveMessage, session, cancellationToken: cancellationToken);
        conversationStore.SetSession(conversationId, session);

        var text = response.ToString() ?? string.Empty;
        if (ShouldFallbackToDirectTaskCreation(routingMessage, text))
        {
            var fallback = await TryCreateTaskDirectlyAsync(routingMessage, conversationId, settings, cancellationToken);
            if (fallback != null)
            {
                return fallback;
            }
        }

        await auditService.LogAsync(
            "agent.chat",
            $"AI assistant processed chat message for conversation {conversationId}.",
            "Succeeded",
            request.Message,
            text,
            settings.ProviderType.ToString(),
            settings.ModelId,
            cancellationToken);

        RememberConversation(conversationId, request.Message, text);

        return new AiChatResponse
        {
            ConversationId = conversationId,
            Message = text,
            ProviderLabel = settings.ProviderType.ToString(),
            ModelId = settings.ModelId,
            RequiresApproval = text.Contains("Approval required", StringComparison.OrdinalIgnoreCase)
        };
    }

    public async IAsyncEnumerable<string> StreamAsync(AiChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = await aiSettingsService.GetAsync(cancellationToken);
        using var chatClient = await aiProviderFactory.CreateChatClientAsync(cancellationToken);
        var agent = CreateAgent(chatClient, settings);
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("n")
            : request.ConversationId!;
        var session = await GetOrCreateSessionAsync(agent, conversationId, cancellationToken);

        await foreach (var update in agent.RunStreamingAsync(request.Message, session, cancellationToken: cancellationToken))
        {
            var text = update.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text;
            }
        }

        conversationStore.SetSession(conversationId, session);
    }

    public void ClearConversation(string conversationId)
    {
        conversationStore.Remove(conversationId);
    }

    private AIAgent CreateAgent(IChatClient chatClient, Models.AiSettings settings)
    {
        IList<AITool>? tools = settings.EnableToolCalling
            ? new AITool[]
            {
                AIFunctionFactory.Create(toolService.GetProjectSummary),
                AIFunctionFactory.Create(toolService.GetTaskSummary),
                AIFunctionFactory.Create(toolService.GetTasksByProject),
                AIFunctionFactory.Create(toolService.GetOverdueTasks),
                AIFunctionFactory.Create(toolService.GetBlockedTasks),
                AIFunctionFactory.Create(toolService.GetUpcomingMilestones),
                AIFunctionFactory.Create(toolService.SearchProjects),
                AIFunctionFactory.Create(toolService.SearchTasks),
                AIFunctionFactory.Create(toolService.FindTaskByName),
                AIFunctionFactory.Create(toolService.CreateProject),
                AIFunctionFactory.Create(toolService.CreateTask),
                AIFunctionFactory.Create(toolService.UpdateTaskDates),
                AIFunctionFactory.Create(toolService.AssignTask),
                AIFunctionFactory.Create(toolService.AddTaskComment),
                AIFunctionFactory.Create(toolService.AddProjectComment),
                AIFunctionFactory.Create(toolService.GetTeamAvailability),
                AIFunctionFactory.Create(toolService.GetUserAssignedTasks),
                AIFunctionFactory.Create(toolService.GetDueSoonTasks),
                AIFunctionFactory.Create(toolService.GetProjectRisks),
                AIFunctionFactory.Create(toolService.GetCalendarContext),
                AIFunctionFactory.Create(toolService.GetGoogleIntegrationStatus),
                AIFunctionFactory.Create(toolService.GetInboxSummary),
                AIFunctionFactory.Create(toolService.SearchInboxEmails),
                AIFunctionFactory.Create(toolService.GetLinkedCalendarEvents),
                AIFunctionFactory.Create(toolService.CalculateNextWorkingSlot),
                AIFunctionFactory.Create(toolService.GetDependencyChain),
                AIFunctionFactory.Create(toolService.GetCurrentDateTime),
                AIFunctionFactory.Create(toolService.GetWorkingCalendarRules),
                AIFunctionFactory.Create(toolService.CreateFollowUpTask),
                AIFunctionFactory.Create(toolService.CreateNotificationScheduleEntry),
                AIFunctionFactory.Create(toolService.CancelNotificationScheduleEntriesForTask),
                AIFunctionFactory.Create(toolService.RecomputeNotificationPlanForTask),
                AIFunctionFactory.Create(toolService.GetNotificationContext),
                AIFunctionFactory.Create(toolService.ConvertTime),
                AIFunctionFactory.Create(toolService.GetFinanceSummary),
                AIFunctionFactory.Create(toolService.GetUpcomingBills),
                AIFunctionFactory.Create(toolService.GetProjectFinanceSummary),
                AIFunctionFactory.Create(toolService.CreateFinanceTransaction),
                AIFunctionFactory.Create(webSearchToolService.web_search)
            }
            : null;

        return chatClient.AsAIAgent(
            instructions: settings.SystemPrompt,
            name: "ProjectManagementAssistant",
            tools: tools);
    }

    private async Task<AgentSession> GetOrCreateSessionAsync(AIAgent agent, string conversationId, CancellationToken cancellationToken)
    {
        var existing = conversationStore.GetOrCreate(conversationId).Session;
        if (existing != null)
        {
            return existing;
        }

        var session = await agent.CreateSessionAsync(cancellationToken);
        conversationStore.SetSession(conversationId, session);
        return session;
    }

    private static bool ShouldRouteDirectTaskCreation(string userMessage)
    {
        return Regex.IsMatch(userMessage, @"\b(create|add|schedule)\b", RegexOptions.IgnoreCase)
            && Regex.IsMatch(userMessage, @"\b(task|meeting|reminder)\b", RegexOptions.IgnoreCase)
            && !string.IsNullOrWhiteSpace(ExtractTitle(userMessage));
    }

    private static string ExtractCurrentUserRequest(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var matches = Regex.Matches(message, @"(?:^|\r?\n)Current user request:\s*(?<request>.*?)(?=\r?\n[A-Z][A-Za-z ]+?:|\z)", RegexOptions.Singleline);
        if (matches.Count == 0)
        {
            return message.Trim();
        }

        return matches[^1].Groups["request"].Value.Trim();
    }

    private static bool ShouldRouteDirectTaskLookup(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return false;
        }

        if (ShouldRouteDirectTaskCreation(userMessage))
        {
            return false;
        }

        return Regex.IsMatch(userMessage, @"\b(when|what|which|show|tell|find|lookup|look up)\b", RegexOptions.IgnoreCase)
            && Regex.IsMatch(userMessage, @"\b(expire|expired|expiry|due|end|ended|finish|finished|complete|completed|status|blocked|overdue|start|started)\b", RegexOptions.IgnoreCase);
    }

    private static bool ShouldFallbackToDirectTaskCreation(string userMessage, string agentResponse)
    {
        if (!ShouldRouteDirectTaskCreation(userMessage))
        {
            return false;
        }

        if (Regex.IsMatch(agentResponse, @"created task\s+\d+", RegexOptions.IgnoreCase))
        {
            return false;
        }

        return Regex.IsMatch(agentResponse, @"unable to create|can't create|cannot create|guide you through|manually|use the tool|schedule a task|\""name\""\s*:\s*\""CreateTask\""", RegexOptions.IgnoreCase);
    }

    private async Task<AiChatResponse?> TryCreateTaskDirectlyAsync(string message, string conversationId, Models.AiSettings settings, CancellationToken cancellationToken)
    {
        var title = ExtractTitle(message);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var startHint = ExtractStartHint(message);
        var wantsNotification = Regex.IsMatch(message, @"\b(notification|notify|reminder)\b", RegexOptions.IgnoreCase);
        var toolCallPayload = new
        {
            name = "CreateTask",
            parameters = new
            {
                allDay = false,
                assignee = currentUserService.IsAuthenticated ? currentUserService.UserId : null,
                categoryId = (int?)null,
                dependsOnTaskId = (int?)null,
                description = Regex.IsMatch(message, @"\bmeeting\b", RegexOptions.IgnoreCase) ? "Created from AI meeting request." : (string?)null,
                projectId = (int?)null,
                projectName = (string?)null,
                stageId = (int?)null,
                stageName = (string?)null,
                startHint,
                title
            }
        };
        var created = await toolService.CreateTask(
            title: title,
            description: Regex.IsMatch(message, @"\bmeeting\b", RegexOptions.IgnoreCase) ? "Created from AI meeting request." : null,
            assignee: currentUserService.IsAuthenticated ? currentUserService.UserId : null,
            startHint: startHint,
            allDay: false,
            cancellationToken: cancellationToken);

        if (!created.StartsWith("Created task", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var messageText = wantsNotification
            ? $"{created} Pre-event reminders will be scheduled for the assigned user when proactive reminders are enabled."
            : created;

        await auditService.LogAsync(
            "agent.chat.fallback-create-task",
            "Created task through deterministic fallback.",
            "Succeeded",
            message,
            messageText,
            settings.ProviderType.ToString(),
            settings.ModelId,
            cancellationToken);

        RememberConversation(conversationId, message, messageText);
        conversationStore.AddFact(conversationId, $"Last created task: {created}");

        return new AiChatResponse
        {
            ConversationId = conversationId,
            Message = messageText,
            ProviderLabel = settings.ProviderType.ToString(),
            ModelId = settings.ModelId,
            RequiresApproval = false,
            Actions =
            [
                new AiActionItemViewModel
                {
                    Title = "Tool Call Attempt",
                    Detail = JsonSerializer.Serialize(toolCallPayload, new JsonSerializerOptions { WriteIndented = true }),
                    Status = "info"
                },
                new AiActionItemViewModel
                {
                    Title = "Tool Result",
                    Detail = created,
                    Status = "success"
                }
            ]
        };
    }

    private async Task<AiChatResponse?> TryAnswerTaskLookupDirectlyAsync(string message, string conversationId, Models.AiSettings settings, CancellationToken cancellationToken)
    {
        var lookupQuery = ExtractTaskLookupQuery(message);
        if (string.IsNullOrWhiteSpace(lookupQuery))
        {
            return null;
        }

        var match = await toolService.FindBestTaskMatchAsync(lookupQuery, cancellationToken);
        if (match == null)
        {
            return null;
        }

        var responseText = BuildTaskLookupResponse(message, match);

        await auditService.LogAsync(
            "agent.chat.direct-task-lookup",
            "Answered task lookup through deterministic search.",
            "Succeeded",
            message,
            responseText,
            settings.ProviderType.ToString(),
            settings.ModelId,
            cancellationToken);

        RememberConversation(conversationId, message, responseText);
        conversationStore.AddFact(conversationId, $"Last matched task: {match.TaskId}:{match.Title}");

        return new AiChatResponse
        {
            ConversationId = conversationId,
            Message = responseText,
            ProviderLabel = settings.ProviderType.ToString(),
            ModelId = settings.ModelId,
            RequiresApproval = false,
            Actions =
            [
                new AiActionItemViewModel
                {
                    Title = "Matched Task",
                    Detail = $"{match.TaskId}:{match.Title} [{match.Status}] due {match.EndDateTime:yyyy-MM-dd HH:mm} project={match.ProjectName ?? "None"}",
                    Status = "success"
                }
            ]
        };
    }

    private void RememberConversation(string conversationId, string userMessage, string assistantMessage)
    {
        conversationStore.AddTurn(conversationId, "user", userMessage);
        conversationStore.AddTurn(conversationId, "assistant", assistantMessage);
    }

    private static string BuildEffectiveMessage(string userMessage, AiConversationState conversationState, Models.AiSettings settings)
    {
        var contextBuilder = new List<string>();
        
        try
        {
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(settings.DefaultTimeZone);
            var localTime = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, tzInfo);
            contextBuilder.Add($"System Current Local Time ({tzInfo.Id}): {localTime:yyyy-MM-dd hh:mm tt}. Use this as the reference context for 'now', 'today', 'local time'.");
        }
        catch 
        {
            contextBuilder.Add($"System Current Time (Server Local): {DateTimeOffset.Now:yyyy-MM-dd hh:mm tt}. Use this as the reference context for 'now', 'today'.");
        }

        if (conversationState.Facts.Count > 0)
        {
            contextBuilder.Add("Remembered facts:");
            contextBuilder.AddRange(conversationState.Facts.Select(fact => $"- {fact}"));
        }

        if (conversationState.Turns.Count > 0)
        {
            contextBuilder.Add("Recent conversation:");
            foreach (var turn in conversationState.Turns.TakeLast(6))
            {
                contextBuilder.Add($"{turn.Role}: {turn.Message}");
            }
        }

        if (contextBuilder.Count == 0)
        {
            return userMessage;
        }

        contextBuilder.Add($"Current user request: {userMessage}");
        contextBuilder.Add("Routing rules: if the user asks about a task name/title with words like due, expired, expiry, end, overdue, blocked, completed, or status, call SearchTasks using only the likely task name keywords first, then answer from the matching task result. Example: 'when our Rejo expired' -> SearchTasks('Rejo').");
        contextBuilder.Add("Web search rules: call SearchAsync for latest, current, recent, news, prices, public laws/rules, product versions, software versions, public documentation, public websites, or explicit prompts like search Google, look up, or find online.");
        contextBuilder.Add("Do not use SearchAsync for project tasks, email, calendar, local files, private user data, or anything already available from Zero tools.");
        contextBuilder.Add("After SearchAsync, answer only from the returned web results, mention source titles or URLs, and say when the results are weak or insufficient.");
        contextBuilder.Add("Use the remembered facts only when they match live tool results or recent confirmed actions.");
        return string.Join(Environment.NewLine, contextBuilder);
    }

    private static string? ExtractTitle(string message)
    {
        var quoted = Regex.Match(message, "\"([^\"]+)\"");
        if (quoted.Success)
        {
            return quoted.Groups[1].Value.Trim();
        }

        var titlePattern = Regex.Match(message, @"title\s+(?:is\s+)?(?<title>.+?)(?:\s+(?:at|for|on|next|tomorrow|with|and)\b|$)", RegexOptions.IgnoreCase);
        if (titlePattern.Success)
        {
            return titlePattern.Groups["title"].Value.Trim(' ', '.', '"');
        }

        var createPattern = Regex.Match(message, @"(?:create|add|schedule)\s+(?:a\s+)?(?:meeting\s+)?(?:task\s+)?(?<title>.+?)(?:\s+(?:at|for|on|next|tomorrow|with|and)\b|$)", RegexOptions.IgnoreCase);
        return createPattern.Success ? createPattern.Groups["title"].Value.Trim(' ', '.', '"') : null;
    }

    private static string? ExtractStartHint(string message)
    {
        var match = Regex.Match(message, @"\b(?:at|for)\s+(?<time>.+)$", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["time"].Value.Trim() : null;
    }

    private static string? ExtractTaskLookupQuery(string message)
    {
        var quoted = Regex.Match(message, "\"([^\"]+)\"");
        if (quoted.Success)
        {
            return quoted.Groups[1].Value.Trim();
        }

        var patterns = new[]
        {
            @"(?:when|what|which|show|tell|find|lookup|look up)\s+(?:is|was|did|does)?\s*(?:our|the|a|an)?\s*(?<name>.+?)\s+(?:expire|expired|expiry|due|end|ended|finish|finished|complete|completed|status|blocked|overdue|start|started)\b",
            @"(?<name>.+?)\s+(?:expire|expired|expiry|due|end|ended|finish|finished|complete|completed|status|blocked|overdue|start|started)\b"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups["name"].Value.Trim(' ', '.', '?', '!', '"', '\'');
            }
        }

        return null;
    }

    private static string BuildTaskLookupResponse(string originalMessage, TaskLookupResult match)
    {
        var isExpiryQuestion = Regex.IsMatch(originalMessage, @"\b(expire|expired|expiry|due|end|ended)\b", RegexOptions.IgnoreCase);
        var isStatusQuestion = Regex.IsMatch(originalMessage, @"\b(status|blocked|complete|completed|overdue)\b", RegexOptions.IgnoreCase);

        if (isExpiryQuestion)
        {
            if (match.IsOverdue)
            {
                return $"{match.Title} was due on {match.EndDateTime:yyyy-MM-dd HH:mm} and is currently overdue. Status: {match.Status}.";
            }

            return $"{match.Title} is scheduled to end on {match.EndDateTime:yyyy-MM-dd HH:mm}. Current status: {match.Status}.";
        }

        if (isStatusQuestion)
        {
            return $"{match.Title} is currently {match.Status} and is due on {match.EndDateTime:yyyy-MM-dd HH:mm}.";
        }

        return $"{match.Title} is scheduled from {match.StartDateTime:yyyy-MM-dd HH:mm} to {match.EndDateTime:yyyy-MM-dd HH:mm}. Current status: {match.Status}.";
    }
}
