using System.ComponentModel.DataAnnotations;

namespace MedInsuranceHelper.Api.Models;

/// <summary>Inbound body for POST /api/chat.</summary>
public class ChatRequest
{
    /// <summary>Existing session ID; null or absent to start a new session.</summary>
    public string? SessionId { get; set; }

    /// <summary>Optional application user identifier.</summary>
    public string? UserId { get; set; }

    /// <summary>The user's natural-language message.</summary>
    [Required]
    public ChatMessageInput Message { get; set; } = default!;

    /// <summary>Optional free-form context hints (locale, plan-filter, etc.).</summary>
    public Dictionary<string, string>? ContextHints { get; set; }
}

/// <summary>The user-typed message body.</summary>
public class ChatMessageInput
{
    [Required, MinLength(1)]
    public string Text { get; set; } = string.Empty;
}

/// <summary>Response envelope for POST /api/chat.</summary>
public class ChatResponse
{
    public string SessionId { get; set; } = string.Empty;
    public ResponseArtifact ResponseArtifact { get; set; } = new();
}

/// <summary>Response envelope for POST /api/chat (Foundry RAG integration).</summary>
public class ChatResponseV2
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<SourceCitation> SourceCitations { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
