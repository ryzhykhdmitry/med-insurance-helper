namespace MedInsuranceHelper.Api.Models;

/// <summary>An active conversation session.</summary>
public class ConversationSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Expiration timestamp (LastActiveAt + 30 minutes). Session is removed if ExpiresAt &lt; UtcNow.</summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(30);

    /// <summary>Optional metadata (user preferences, context hints). Stored as a dictionary.</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>Ordered list of messages in this session.</summary>
    public List<Message> Messages { get; set; } = new();
}
