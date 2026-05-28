using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using MedInsuranceHelper.Api.Configuration;
using MedInsuranceHelper.Api.Models;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace MedInsuranceHelper.Api.Services;

/// <summary>Request sent to Azure AI Foundry RAG endpoint.</summary>
public record FoundryRagRequest
{
    public required string UserQuery { get; init; }
    public List<Message> ConversationHistory { get; init; } = new();
    public int TopK { get; init; } = 5;
}

/// <summary>Response from Azure AI Foundry RAG endpoint.</summary>
public record FoundryRagResponse
{
    public required string GeneratedAnswer { get; init; }
    public List<SourceCitation> SourceCitations { get; init; } = new();
    public DateTime ResponseTimestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>Service for RAG-powered chat using Azure AI Search and Azure OpenAI.</summary>
public interface IFoundryRagService
{
    Task<FoundryRagResponse> ChatAsync(FoundryRagRequest request, CancellationToken ct = default);
}

/// <summary>
/// Implements RAG orchestration:
/// 1. Generate query embedding
/// 2. Search Azure AI Search vector index
/// 3. Assemble prompt with retrieved context
/// 4. Call Azure OpenAI for response generation
/// 5. Return answer with source citations
/// </summary>
public class FoundryRagService : IFoundryRagService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IFoundryClient _foundryClient;
    private readonly SearchClient _searchClient;
    private readonly ILogger<FoundryRagService> _logger;
    private readonly AppSettings _settings;

    public FoundryRagService(
        IEmbeddingService embeddingService,
        IFoundryClient foundryClient,
        IOptions<AppSettings> options,
        ILogger<FoundryRagService> logger)
    {
        _embeddingService = embeddingService;
        _foundryClient = foundryClient;
        _logger = logger;
        _settings = options.Value;

        // Initialize Azure AI Search client
        if (string.IsNullOrEmpty(_settings.SearchEndpoint) || string.IsNullOrEmpty(_settings.SearchKey))
        {
            _logger.LogWarning("Azure AI Search not configured. RAG functionality will be limited.");
        }
        else
        {
            var searchCredential = new AzureKeyCredential(_settings.SearchKey);
            _searchClient = new SearchClient(
                new Uri(_settings.SearchEndpoint),
                _settings.SearchIndexName,
                searchCredential);
        }
    }

    public async Task<FoundryRagResponse> ChatAsync(FoundryRagRequest request, CancellationToken ct = default)
    {
        try
        {
            // Step 1: Generate query embedding
            _logger.LogInformation("Generating query embedding for: {Query}", request.UserQuery);
            var queryEmbedding = await _embeddingService.EmbedAsync(request.UserQuery, ct);

            // Step 2: Search Azure AI Search index (hybrid search: vector + keyword)
            _logger.LogInformation("Searching Azure AI Search index with top {TopK} results", request.TopK);
            var searchResults = await SearchDocumentsAsync(request.UserQuery, queryEmbedding, request.TopK, ct);

            // Step 3: Assemble prompt with retrieved context
            var systemPrompt = BuildSystemPrompt(searchResults);
            var chatMessages = BuildChatMessages(systemPrompt, request.ConversationHistory, request.UserQuery);

            // Step 4: Call Azure OpenAI for response generation
            _logger.LogInformation("Calling Azure OpenAI for response generation");
            var answer = await _foundryClient.CompleteChatAsync(chatMessages, ct);

            // Step 5: Return response with source citations
            return new FoundryRagResponse
            {
                GeneratedAnswer = answer,
                SourceCitations = searchResults,
                ResponseTimestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RAG chat processing");
            throw new InvalidOperationException("Azure AI Foundry service unavailable. Please try again later.", ex);
        }
    }

    private async Task<List<SourceCitation>> SearchDocumentsAsync(
        string query,
        float[] queryEmbedding,
        int topK,
        CancellationToken ct)
    {
        if (_searchClient == null)
        {
            _logger.LogWarning("Search client not initialized. Returning empty results.");
            return new List<SourceCitation>();
        }

        try
        {
            // Fetch a wider candidate pool so we can filter by most relevant files
            int candidateSize = topK * 3;

            var vectorQuery = new VectorizedQuery(queryEmbedding)
            {
                KNearestNeighborsCount = candidateSize,
                Fields = { "contentVector" }
            };

            var searchOptions = new SearchOptions
            {
                VectorSearch = new VectorSearchOptions
                {
                    Queries = { vectorQuery }
                },
                Size = candidateSize,
                Select = { "id", "content", "fileName", "blobUri", "chunkIndex", "pageNumber" }
            };

            var response = await _searchClient.SearchAsync<SearchDocument>(query, searchOptions, ct);
            var candidates = new List<SourceCitation>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                candidates.Add(new SourceCitation
                {
                    DocumentId = result.Document.TryGetValue("blobUri", out var uri) ? uri?.ToString() ?? "" : "",
                    Content = result.Document.TryGetValue("content", out var content) ? content?.ToString() ?? "" : "",
                    FileName = result.Document.TryGetValue("fileName", out var fileName) ? fileName?.ToString() ?? "" : "",
                    PageNumber = result.Document.TryGetValue("pageNumber", out var page) && page != null ? Convert.ToInt32(page) : null,
                    ChunkIndex = result.Document.TryGetValue("chunkIndex", out var chunk) && chunk != null ? Convert.ToInt32(chunk) : 0,
                    Score = result.Score ?? 0.0
                });
            }

            // Rank files by their total score across all retrieved chunks.
            // Keep top 2 files so single-plan queries return one file and
            // comparison queries ("compare X and Y") return two files.
            var topFiles = candidates
                .GroupBy(c => c.FileName)
                .OrderByDescending(g => g.Sum(c => c.Score))
                .Take(2)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var citations = candidates
                .Where(c => topFiles.Contains(c.FileName))
                .OrderByDescending(c => c.Score)
                .Take(topK)
                .ToList();

            _logger.LogInformation(
                "Retrieved {Total} candidates; kept {Count} chunks from {Files} file(s): {FileNames}",
                candidates.Count, citations.Count, topFiles.Count, string.Join(", ", topFiles));

            return citations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Azure AI Search index");
            return new List<SourceCitation>();
        }
    }

    private string BuildSystemPrompt(List<SourceCitation> citations)
    {
        if (citations.Count == 0)
        {
            return @"You are a helpful assistant for medical insurance questions. 
Answer based on your general knowledge. If you don't know, say so.";
        }

        var context = string.Join("\n\n", citations.Select((c, i) =>
            $"[{i + 1}] From {c.FileName} (Chunk {c.ChunkIndex}):\n{c.Content}"));

        return $@"You are a helpful assistant for medical insurance questions.
Answer questions using the following document excerpts as your primary source.

Retrieved Documents:
{context}

Instructions:
- Answer concisely and accurately based on the provided documents
- Cite document numbers [1], [2], etc. when referencing specific information
- If the documents cover the topic partially, answer with what is available and note what is missing
- Only say you don't have information if the topic is completely absent from all provided documents
- Do not make up facts not present in the documents";
    }

    private List<ChatMessage> BuildChatMessages(
        string systemPrompt,
        List<Message> conversationHistory,
        string currentQuery)
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(systemPrompt)
        };

        // Add conversation history (limit to last 10 messages to avoid token limits)
        var recentHistory = conversationHistory.TakeLast(10);
        foreach (var msg in recentHistory)
        {
            if (msg.Role == MessageRole.User)
            {
                messages.Add(ChatMessage.CreateUserMessage(msg.Text));
            }
            else if (msg.Role == MessageRole.Assistant)
            {
                messages.Add(ChatMessage.CreateAssistantMessage(msg.Text));
            }
        }

        // Add current user query
        messages.Add(ChatMessage.CreateUserMessage(currentQuery));

        return messages;
    }
}
