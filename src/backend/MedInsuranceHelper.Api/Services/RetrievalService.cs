using MedInsuranceHelper.Api.Configuration;
using MedInsuranceHelper.Api.Models;
using MedInsuranceHelper.Api.Services.VectorStore;
using Microsoft.Extensions.Options;

namespace MedInsuranceHelper.Api.Services;

/// <summary>A retrieved chunk with its relevance score.</summary>
public record RetrievedChunk(string OfferId, string ChunkId, string Text, int StartPage, int EndPage, double Score);

/// <summary>Orchestrates embedding + vector store lookup to produce ranked chunk results.</summary>
public interface IRetrievalService
{
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query, int topK, string? filterOfferId = null, CancellationToken ct = default);
}

public class RetrievalService : IRetrievalService
{
    private readonly IEmbeddingService _embedder;
    private readonly IFileVectorStore _vectorStore;
    private readonly double _minScore;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(
        IEmbeddingService embedder,
        IFileVectorStore vectorStore,
        IOptions<AppSettings> options,
        ILogger<RetrievalService> logger)
    {
        _embedder = embedder;
        _vectorStore = vectorStore;
        _minScore = options.Value.MinRelevanceScore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query, int topK, string? filterOfferId = null, CancellationToken ct = default)
    {
        var queryEmbedding = await _embedder.EmbedAsync(query, ct);
        var raw = await _vectorStore.SearchAsync(queryEmbedding, topK, filterOfferId, ct);

        var results = raw
            .Where(r => r.Score >= _minScore)
            .Select(r => new RetrievedChunk(r.OfferId, r.ChunkId, r.Text, r.StartPage, r.EndPage, r.Score))
            .ToList();

        _logger.LogInformation("Retrieved {Count}/{Total} chunks above threshold {Threshold} for query '{Query}'.",
            results.Count, raw.Count, _minScore, query);
        return results;
    }
}
