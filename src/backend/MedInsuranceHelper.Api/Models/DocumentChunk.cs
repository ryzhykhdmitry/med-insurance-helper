namespace MedInsuranceHelper.Api.Models;

/// <summary>A text chunk extracted from an insurance offer PDF, with embedding reference.</summary>
public class DocumentChunk
{
    /// <summary>Unique identifier (UUID).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Foreign key to <see cref="InsuranceOffer.Id"/>.</summary>
    public string OfferId { get; set; } = string.Empty;

    /// <summary>Extracted text content of this chunk.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>First page number this chunk covers (1-based).</summary>
    public int StartPage { get; set; }

    /// <summary>Last page number this chunk covers (1-based).</summary>
    public int EndPage { get; set; }

    /// <summary>Character offset within the full document text.</summary>
    public int Offset { get; set; }

    /// <summary>Character length of this chunk.</summary>
    public int Length { get; set; }

    /// <summary>
    /// The embedding vector for this chunk. Stored inline in the local JSON vector store.
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>Timestamp when this chunk was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
