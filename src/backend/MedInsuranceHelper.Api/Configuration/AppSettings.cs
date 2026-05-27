namespace MedInsuranceHelper.Api.Configuration;

/// <summary>
/// Strongly-typed application settings. Bound from appsettings.json and environment variables.
/// </summary>
public class AppSettings
{
    public const string SectionName = "AppSettings";

    /// <summary>Azure AI Foundry API key.</summary>
    public string FoundryApiKey { get; set; } = string.Empty;

    /// <summary>Azure AI Foundry endpoint URL (e.g. https://my-endpoint.openai.azure.com/).</summary>
    public string FoundryEndpoint { get; set; } = string.Empty;
    
    /// <summary>Azure AI Foundry project endpoint for RAG.</summary>
    public string ProjectEndpoint { get; set; } = string.Empty;
    
    /// <summary>Azure AI Foundry RAG deployment name.</summary>
    public string RagDeploymentName { get; set; } = "chat-with-data";
    
    /// <summary>Azure AI Search endpoint URL.</summary>
    public string SearchEndpoint { get; set; } = string.Empty;
    
    /// <summary>Azure AI Search API key.</summary>
    public string SearchKey { get; set; } = string.Empty;
    
    /// <summary>Azure AI Search index name for insurance documents.</summary>
    public string SearchIndexName { get; set; } = "insurance-documents";

    /// <summary>Azure Blob Storage connection string (Azurite for local dev).</summary>
    public string BlobConnectionString { get; set; } = string.Empty;

    /// <summary>Azure Blob container name for insurance PDFs.</summary>
    public string BlobContainerName { get; set; } = "insurance-pdfs";

    /// <summary>Deployment name for the embedding model in Azure AI Foundry.</summary>
    public string EmbeddingDeployment { get; set; } = "text-embedding-ada-002";

    /// <summary>Deployment name for the chat completion model in Azure AI Foundry.</summary>
    public string ChatDeployment { get; set; } = "gpt-4o";

    /// <summary>Local directory for vector store JSON files.</summary>
    public string VectorStoreDirectory { get; set; } = "data/vectors";

    /// <summary>Chunk size (in characters) for the sliding-window chunker.</summary>
    public int ChunkSize { get; set; } = 800;

    /// <summary>Overlap (in characters) between successive chunks.</summary>
    public int ChunkOverlap { get; set; } = 200;

    /// <summary>Default number of top chunks returned by vector search.</summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>Minimum cosine-similarity score for a chunk to be considered relevant.</summary>
    public double MinRelevanceScore { get; set; } = 0.75;

    /// <summary>Application environment (Development / Staging / Production).</summary>
    public string AppEnv { get; set; } = "Development";

    /// <summary>
    /// Policy for handling single-plan comparison requests.
    /// "clarify" (default): ask the user for a second plan name.
    /// "recommend": auto-convert to a recommendation request.
    /// </summary>
    public string SinglePlanComparePolicy { get; set; } = "clarify";
}
