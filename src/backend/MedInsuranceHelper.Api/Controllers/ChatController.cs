using MedInsuranceHelper.Api.Models;
using MedInsuranceHelper.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedInsuranceHelper.Api.Controllers;

/// <summary>
/// POST /api/chat — unified natural-language chat endpoint.
/// Supports both legacy orchestration and new Foundry RAG integration.
/// </summary>
[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatOrchestrationService _orchestration;
    private readonly IFoundryRagService _foundryRag;
    private readonly ISessionService _sessions;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatOrchestrationService orchestration,
        IFoundryRagService foundryRag,
        ISessionService sessions,
        ILogger<ChatController> logger)
    {
        _orchestration = orchestration;
        _foundryRag = foundryRag;
        _sessions = sessions;
        _logger = logger;
    }

    /// <summary>
    /// Handles a natural-language message from the user using Foundry RAG.
    /// Creates a new session if <c>sessionId</c> is absent or unknown.
    /// Returns response with source citations and session expiration.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponseV2), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        // Validate input
        if (request?.Message == null || string.IsNullOrWhiteSpace(request.Message.Text))
            return BadRequest(new { error = "message.text is required." });

        if (request.Message.Text.Length > 2000)
            return BadRequest(new { error = "Message exceeds maximum length of 2000 characters." });

        // Get or create session
        var session = _sessions.CreateOrGet(request.SessionId);
        
        // Check if session is expired (CreateOrGet returns null if expired and not found)
        if (session == null)
        {
            return NotFound(new 
            { 
                error = new 
                { 
                    code = "SESSION_EXPIRED",
                    message = $"Session {request.SessionId} expired or not found",
                    timestamp = DateTime.UtcNow,
                    requestId = Guid.NewGuid().ToString()
                }
            });
        }

        _logger.LogInformation(
            "Chat request — session {SessionId}, message length {Length}.",
            session.Id, request.Message.Text.Length);

        try
        {
            // Create user message
            var userMessage = new Message
            {
                Role = MessageRole.User,
                Text = request.Message.Text,
                CreatedAt = DateTime.UtcNow
            };
            _sessions.AppendMessage(session.Id, userMessage);

            // Forward to Foundry RAG service
            var ragRequest = new FoundryRagRequest
            {
                UserQuery = request.Message.Text,
                ConversationHistory = session.Messages.ToList(),
                TopK = 5
            };

            var ragResponse = await _foundryRag.ChatAsync(ragRequest, ct);

            // Create assistant message with citations
            var assistantMessage = new Message
            {
                Role = MessageRole.Assistant,
                Text = ragResponse.GeneratedAnswer,
                CreatedAt = DateTime.UtcNow,
                SourceCitations = ragResponse.SourceCitations
            };
            _sessions.AppendMessage(session.Id, assistantMessage);

            return Ok(new ChatResponseV2
            {
                SessionId = session.Id,
                Message = ragResponse.GeneratedAnswer,
                SourceCitations = ragResponse.SourceCitations,
                Timestamp = ragResponse.ResponseTimestamp,
                ExpiresAt = session.ExpiresAt
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unavailable"))
        {
            _logger.LogError(ex, "Foundry service unavailable");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = new
                {
                    code = "SERVICE_UNAVAILABLE",
                    message = "Azure AI Foundry unavailable",
                    timestamp = DateTime.UtcNow,
                    requestId = Guid.NewGuid().ToString()
                }
            });
        }
    }
    
    /// <summary>
    /// Legacy endpoint using ChatOrchestrationService (deprecated).
    /// Use POST /api/chat instead.
    /// </summary>
    [HttpPost("legacy")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChatLegacy([FromBody] ChatRequest request, CancellationToken ct)
    {
        if (request?.Message == null || string.IsNullOrWhiteSpace(request.Message.Text))
            return BadRequest(new { error = "message.text is required." });

        var session = _sessions.CreateOrGet(request.SessionId);

        _logger.LogInformation(
            "Legacy chat request — session {SessionId}, message length {Length}.",
            session.Id, request.Message.Text.Length);

        var artifact = await _orchestration.HandleAsync(request.Message.Text, session, ct);

        return Ok(new ChatResponse
        {
            SessionId = session.Id,
            ResponseArtifact = artifact
        });
    }
}
