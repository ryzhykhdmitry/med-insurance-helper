namespace MedInsuranceHelper.Api.Models;

/// <summary>Role of a conversation participant.</summary>
public enum MessageRole { User, Assistant, System }

/// <summary>A single message within a <see cref="ConversationSession"/>.</summary>
public class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public MessageRole Role { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Citations attached to assistant messages.</summary>
    public List<Citation> Citations { get; set; } = new();
}

/// <summary>A source citation produced during RAG.</summary>
public class Citation
{
    public string DocumentId { get; set; } = string.Empty;
    public string ChunkId { get; set; } = string.Empty;
    public string PageRef { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
}
