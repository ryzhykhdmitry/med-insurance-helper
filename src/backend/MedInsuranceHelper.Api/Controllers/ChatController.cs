using MedInsuranceHelper.Api.Models;
using MedInsuranceHelper.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedInsuranceHelper.Api.Controllers;

/// <summary>
/// POST /api/chat — unified natural-language chat endpoint.
/// Replaces the separate /api/compare and /api/recommend endpoints.
/// Detects one or more intents, orchestrates the appropriate pipelines,
/// and returns a single <see cref="ChatResponse"/> with labelled sections.
/// </summary>
[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatOrchestrationService _orchestration;
    private readonly ISessionService _sessions;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatOrchestrationService orchestration,
        ISessionService sessions,
        ILogger<ChatController> logger)
    {
        _orchestration = orchestration;
        _sessions = sessions;
        _logger = logger;
    }

    /// <summary>
    /// Handles a natural-language message from the user.
    /// Creates a new session if <c>sessionId</c> is absent or unknown.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        if (request?.Message == null || string.IsNullOrWhiteSpace(request.Message.Text))
            return BadRequest(new { error = "message.text is required." });

        // Create or resume session (FR-004 — context retained until explicit reset)
        var session = _sessions.CreateOrGet(request.SessionId);

        _logger.LogInformation(
            "Chat request — session {SessionId}, message length {Length}.",
            session.Id, request.Message.Text.Length);

        var artifact = await _orchestration.HandleAsync(request.Message.Text, session, ct);

        return Ok(new ChatResponse
        {
            SessionId = session.Id,
            ResponseArtifact = artifact
        });
    }
}
