using MedInsuranceHelper.Api.Models;

namespace MedInsuranceHelper.Api.Services;

/// <summary>Manages in-memory conversation sessions.</summary>
public interface ISessionService
{
    ConversationSession CreateOrGet(string? sessionId);
    ConversationSession? Get(string sessionId);
    void AppendMessage(string sessionId, Message message);
    IReadOnlyList<Message> GetHistory(string sessionId);
    void RemoveExpiredSessions();
    bool Delete(string sessionId);
}

/// <summary>Thread-safe in-memory session store (v1 — no persistence).</summary>
public class SessionService : ISessionService
{
    private readonly Dictionary<string, ConversationSession> _sessions = new();
    private readonly object _lock = new();
    private readonly ILogger<SessionService> _logger;
    
    private const int SessionTimeoutMinutes = 30;

    public SessionService(ILogger<SessionService> logger) => _logger = logger;

    /// <inheritdoc/>
    public ConversationSession CreateOrGet(string? sessionId)
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(sessionId) && _sessions.TryGetValue(sessionId, out var existing))
            {
                // Check if session is expired
                if (existing.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogInformation("Session {SessionId} expired, removing.", sessionId);
                    _sessions.Remove(sessionId);
                    // Create new session with same ID
                    var newSession = new ConversationSession { Id = sessionId };
                    newSession.LastActiveAt = DateTime.UtcNow;
                    newSession.ExpiresAt = DateTime.UtcNow.AddMinutes(SessionTimeoutMinutes);
                    _sessions[newSession.Id] = newSession;
                    _logger.LogInformation("Created new session {SessionId} after expiration.", newSession.Id);
                    return newSession;
                }
                
                // Update activity and expiration
                existing.LastActiveAt = DateTime.UtcNow;
                existing.ExpiresAt = DateTime.UtcNow.AddMinutes(SessionTimeoutMinutes);
                return existing;
            }

            var session = new ConversationSession();
            session.LastActiveAt = DateTime.UtcNow;
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(SessionTimeoutMinutes);
            _sessions[session.Id] = session;
            _logger.LogInformation("Created session {SessionId}.", session.Id);
            return session;
        }
    }

    /// <inheritdoc/>
    public ConversationSession? Get(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                // Check expiration
                if (session.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogInformation("Session {SessionId} expired on retrieval.", sessionId);
                    return null;
                }
                return session;
            }
            return null;
        }
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
                session.ExpiresAt = DateTime.UtcNow.AddMinutes(SessionTimeoutMinutes);
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
    
    /// <inheritdoc/>
    public void RemoveExpiredSessions()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var expiredSessionIds = _sessions
                .Where(kvp => kvp.Value.ExpiresAt < now)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var sessionId in expiredSessionIds)
            {
                _sessions.Remove(sessionId);
                _logger.LogInformation("Removed expired session {SessionId}.", sessionId);
            }
            
            if (expiredSessionIds.Count > 0)
            {
                _logger.LogInformation("Cleanup: removed {Count} expired sessions.", expiredSessionIds.Count);
            }
        }
    }
    
    /// <inheritdoc/>
    public bool Delete(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.Remove(sessionId))
            {
                _logger.LogInformation("Deleted session {SessionId}.", sessionId);
                return true;
            }
            return false;
        }
    }
}
