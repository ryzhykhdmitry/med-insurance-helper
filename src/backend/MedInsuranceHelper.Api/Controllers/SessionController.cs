using MedInsuranceHelper.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedInsuranceHelper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    /// POST /api/session — creates a new session or retrieves an existing one.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    public IActionResult CreateOrGet([FromBody] SessionRequest request)
    {
        var session = _sessions.CreateOrGet(request.SessionId);
        _logger.LogInformation("Session {SessionId} created or retrieved.", session.Id);
        return Ok(new SessionResponse(session.Id));
    }
}

public record SessionRequest(string? SessionId = null);
public record SessionResponse(string SessionId);
