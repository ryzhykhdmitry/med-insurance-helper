using MedInsuranceHelper.Api.Models;
using MedInsuranceHelper.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace MedInsuranceHelper.Api.Controllers;

/// <summary>
/// SSE streaming endpoint — pipes LLM token stream to the client using text/event-stream.
/// At the end of the stream, a citations event is emitted as JSON.
/// Supports multi-turn conversation via sessionId (T034).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StreamController : ControllerBase
{
    private readonly ILLMPipelineService _pipeline;
    private readonly ISessionService _sessions;
    private readonly ILogger<StreamController> _logger;

    public StreamController(ILLMPipelineService pipeline, ISessionService sessions, ILogger<StreamController> logger)
    {
        _pipeline = pipeline;
        _sessions = sessions;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/stream?query=...&amp;topK=5&amp;sessionId=...
    /// Opens an SSE connection; streams tokens then emits a final citations event.
    /// </summary>
    [HttpGet]
    public async Task Stream(
        [FromQuery] string query,
        [FromQuery] int topK = 5,
        [FromQuery] string? sessionId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        // T034: load conversation history for multi-turn context
        IReadOnlyList<(string Role, string Text)>? history = null;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var msgs = _sessions.GetHistory(sessionId);
            history = msgs.Select(m => (m.Role.ToString().ToLowerInvariant(), m.Text)).ToList();
        }

        var result = await _pipeline.RunAsync(query, topK, history, ct);

        // Collect full response text for session storage
        var fullResponse = new StringBuilder();

        // Stream tokens as SSE data events
        await foreach (var token in result.TokenStream.WithCancellation(ct))
        {
            fullResponse.Append(token);
            await WriteEventAsync("token", token, ct);
        }

        // Emit citations at end of stream
        var citationsJson = JsonSerializer.Serialize(result.Citations,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await WriteEventAsync("citations", citationsJson, ct);
        await WriteEventAsync("done", "", ct);
        await Response.Body.FlushAsync(ct);

        // Persist messages to session
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _sessions.AppendMessage(sessionId, new Message
            {
                Role = MessageRole.User, Text = query
            });
            _sessions.AppendMessage(sessionId, new Message
            {
                Role = MessageRole.Assistant,
                Text = fullResponse.ToString(),
                Citations = result.Citations.Select(c => new Citation
                {
                    DocumentId = c.DocumentId, ChunkId = c.ChunkId,
                    PageRef = c.PageRef, Excerpt = c.Excerpt
                }).ToList()
            });
        }

        _logger.LogInformation("SSE stream completed for query '{Query}'. Citations: {Count}.",
            query, result.Citations.Count);
    }

    private async Task WriteEventAsync(string eventType, string data, CancellationToken ct)
    {
        var message = $"event: {eventType}\ndata: {data}\n\n";
        var bytes = Encoding.UTF8.GetBytes(message);
        await Response.Body.WriteAsync(bytes, ct);
        await Response.Body.FlushAsync(ct);
    }
}
