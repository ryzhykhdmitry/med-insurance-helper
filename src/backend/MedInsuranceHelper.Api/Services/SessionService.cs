using MedInsuranceHelper.Api.Models;

namespace MedInsuranceHelper.Api.Services;

/// <summary>Manages in-memory conversation sessions.</summary>
public interface ISessionService
{
    ConversationSession CreateOrGet(string? sessionId);
    ConversationSession? Get(string sessionId);
    void AppendMessage(string sessionId, Message message);
    IReadOnlyList<Message> GetHistory(string sessionId);
}

/// <summary>Thread-safe in-memory session store (v1 — no persistence).</summary>
public class SessionService : ISessionService
{
    private readonly Dictionary<string, ConversationSession> _sessions = new();
    private readonly object _lock = new();
    private readonly ILogger<SessionService> _logger;

    public SessionService(ILogger<SessionService> logger) => _logger = logger;

    /// <inheritdoc/>
    public ConversationSession CreateOrGet(string? sessionId)
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(sessionId) && _sessions.TryGetValue(sessionId, out var existing))
            {
                existing.LastActiveAt = DateTime.UtcNow;
                return existing;
            }

            var session = new ConversationSession();
            _sessions[session.Id] = session;
            _logger.LogInformation("Created session {SessionId}.", session.Id);
            return session;
        }
    }

    /// <inheritdoc/>
    public ConversationSession? Get(string sessionId)
    {
        lock (_lock) return _sessions.TryGetValue(sessionId, out var s) ? s : null;
    }

    /// <inheritdoc/>
    public void AppendMessage(string sessionId, Message message)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                message.SessionId = sessionId;
                session.Messages.Add(message);
                session.LastActiveAt = DateTime.UtcNow;
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Message> GetHistory(string sessionId)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(sessionId, out var s)
                ? s.Messages.AsReadOnly()
                : Array.Empty<Message>();
        }
    }
}
