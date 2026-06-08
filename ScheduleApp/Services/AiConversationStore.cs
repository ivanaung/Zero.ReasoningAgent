using Microsoft.Agents.AI;

namespace ScheduleApp.Services;

public interface IAiConversationStore
{
    AiConversationState GetOrCreate(string conversationId);

    void SetSession(string conversationId, AgentSession session);

    void AddTurn(string conversationId, string role, string message);

    void AddFact(string conversationId, string fact);

    void Remove(string conversationId);
}

public class AiConversationState
{
    public AgentSession? Session { get; set; }

    public List<AiConversationTurn> Turns { get; } = [];

    public List<string> Facts { get; } = [];
}

public class AiConversationTurn
{
    public string Role { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public class AiConversationStore : IAiConversationStore
{
    private readonly Dictionary<string, AiConversationState> _conversations = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public AiConversationState GetOrCreate(string conversationId)
    {
        lock (_lock)
        {
            if (!_conversations.TryGetValue(conversationId, out var state))
            {
                state = new AiConversationState();
                _conversations[conversationId] = state;
            }

            return state;
        }
    }

    public void SetSession(string conversationId, AgentSession session)
    {
        lock (_lock)
        {
            GetOrCreate(conversationId).Session = session;
        }
    }

    public void AddTurn(string conversationId, string role, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        lock (_lock)
        {
            var state = GetOrCreate(conversationId);
            state.Turns.Add(new AiConversationTurn
            {
                Role = role,
                Message = message.Trim()
            });

            if (state.Turns.Count > 12)
            {
                state.Turns.RemoveRange(0, state.Turns.Count - 12);
            }
        }
    }

    public void AddFact(string conversationId, string fact)
    {
        if (string.IsNullOrWhiteSpace(fact))
        {
            return;
        }

        lock (_lock)
        {
            var state = GetOrCreate(conversationId);
            var normalized = fact.Trim();
            state.Facts.RemoveAll(item => string.Equals(item, normalized, StringComparison.Ordinal));
            state.Facts.Insert(0, normalized);

            if (state.Facts.Count > 8)
            {
                state.Facts.RemoveRange(8, state.Facts.Count - 8);
            }
        }
    }

    public void Remove(string conversationId)
    {
        lock (_lock)
        {
            _conversations.Remove(conversationId);
        }
    }
}
