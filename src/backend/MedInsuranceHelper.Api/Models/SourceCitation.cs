namespace MedInsuranceHelper.Api.Models;

/// <summary>
/// Source citation from Azure AI Foundry RAG response.
/// Represents a document chunk retrieved from Azure AI Search and cited in the response.
/// </summary>
public class SourceCitation
{
    /// <summary>Unique document identifier (blob path in Azure Storage).</summary>
    public string DocumentId { get; set; } = string.Empty;
    
    /// <summary>Chunk text content retrieved from search index.</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>Original PDF filename.</summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>Source page number in PDF (if available).</summary>
    public int? PageNumber { get; set; }
    
    /// <summary>Chunk position within document (0-indexed).</summary>
    public int ChunkIndex { get; set; }
    
    /// <summary>Search relevance score (0.0 to 1.0, higher is more relevant).</summary>
    public double Score { get; set; }
}
