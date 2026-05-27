using MedInsuranceHelper.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedInsuranceHelper.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionController : ControllerBase
{
    private readonly ISessionService _sessions;
    private readonly ILogger<SessionController> _logger;

    public SessionController(ISessionService sessions, ILogger<SessionController> logger)
    {
        _sessions = sessions;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/sessions — creates a new session or retrieves an existing one.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    public IActionResult CreateOrGet([FromBody] SessionRequest request)
    {
        var session = _sessions.CreateOrGet(request.SessionId);
        _logger.LogInformation("Session {SessionId} created or retrieved.", session.Id);
        return Ok(new SessionResponse(session.Id));
    }
    
    /// <summary>
    /// GET /api/sessions/{id} — retrieves session metadata (read-only, does not extend expiration).
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SessionMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSession(string id)
    {
        var session = _sessions.Get(id);
        
        if (session == null)
        {
            _logger.LogInformation("Session {SessionId} not found or expired.", id);
            return NotFound(new
            {
                error = new
                {
                    code = "SESSION_NOT_FOUND",
                    message = $"Session {id} expired or not found",
                    timestamp = DateTime.UtcNow
                }
            });
        }
        
        return Ok(new SessionMetadata
        {
            SessionId = session.Id,
            CreatedAt = session.StartedAt,
            LastActiveAt = session.LastActiveAt,
            ExpiresAt = session.ExpiresAt,
            MessageCount = session.Messages.Count
        });
    }
    
    /// <summary>
    /// DELETE /api/sessions/{id} — explicitly ends a conversation session.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteSession(string id)
    {
        var deleted = _sessions.Delete(id);
        
        if (!deleted)
        {
            _logger.LogInformation("Session {SessionId} not found for deletion.", id);
            return NotFound(new
            {
                error = new
                {
                    code = "SESSION_NOT_FOUND",
                    message = $"Session {id} not found",
                    timestamp = DateTime.UtcNow
                }
            });
        }
        
        _logger.LogInformation("Session {SessionId} deleted.", id);
        return NoContent();
    }
}

public record SessionRequest(string? SessionId = null);
public record SessionResponse(string SessionId);

/// <summary>Session metadata response.</summary>
public record SessionMetadata
{
    public string SessionId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime LastActiveAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int MessageCount { get; init; }
}
