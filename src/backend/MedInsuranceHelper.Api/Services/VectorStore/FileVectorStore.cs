using MedInsuranceHelper.Api.Configuration;
using MedInsuranceHelper.Api.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MedInsuranceHelper.Api.Services.VectorStore;

/// <summary>A document chunk entry as stored in the JSON vector file.</summary>
public class VectorEntry
{
    public string ChunkId { get; set; } = string.Empty;
    public string OfferId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int StartPage { get; set; }
    public int EndPage { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

/// <summary>Abstraction for the local file-backed vector store.</summary>
public interface IFileVectorStore
{
    Task UpsertAsync(string offerId, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK, string? filterOfferId = null, CancellationToken ct = default);
    Task DeleteAsync(string offerId, CancellationToken ct = default);
}

/// <summary>Search result from the vector store.</summary>
public record VectorSearchResult(string OfferId, string ChunkId, string Text, int StartPage, int EndPage, double Score);

/// <summary>
/// Local JSON file-backed vector store.
/// Stores one JSON file per offer at data/vectors/{offerId}.json.
/// Retrieval uses cosine similarity with an optional per-offer filter.
/// </summary>
public class FileVectorStore : IFileVectorStore
{
    private readonly string _baseDir;
    private readonly ILogger<FileVectorStore> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FileVectorStore(IOptions<AppSettings> options, ILogger<FileVectorStore> logger)
    {
        _baseDir = options.Value.VectorStoreDirectory;
        _logger = logger;
        Directory.CreateDirectory(_baseDir);
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(string offerId, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default)
    {
        var entries = chunks.Select(c => new VectorEntry
        {
            ChunkId = c.Id,
            OfferId = c.OfferId,
            Text = c.Text,
            StartPage = c.StartPage,
            EndPage = c.EndPage,
            Offset = c.Offset,
            Length = c.Length,
            Embedding = c.Embedding ?? Array.Empty<float>()
        }).ToList();

        var path = GetPath(offerId);
        var json = JsonSerializer.Serialize(entries, JsonOpts);
        await File.WriteAllTextAsync(path, json, ct);
        _logger.LogInformation("Saved {Count} vectors for offer {OfferId} to {Path}.", entries.Count, offerId, path);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding, int topK, string? filterOfferId = null, CancellationToken ct = default)
    {
        var allEntries = new List<VectorEntry>();

        var files = filterOfferId != null
            ? new[] { GetPath(filterOfferId) }.Where(File.Exists)
            : Directory.EnumerateFiles(_baseDir, "*.json");

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var json = await File.ReadAllTextAsync(file, ct);
            var entries = JsonSerializer.Deserialize<List<VectorEntry>>(json, JsonOpts);
            if (entries != null) allEntries.AddRange(entries);
        }

        return allEntries
            .Where(e => e.Embedding.Length > 0)
            .Select(e => new VectorSearchResult(
                e.OfferId, e.ChunkId, e.Text, e.StartPage, e.EndPage,
                CosineSimilarity(queryEmbedding, e.Embedding)))
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string offerId, CancellationToken ct = default)
    {
        var path = GetPath(offerId);
        if (File.Exists(path)) File.Delete(path);
        _logger.LogInformation("Deleted vector store for offer {OfferId}.", offerId);
        return Task.CompletedTask;
    }

    private string GetPath(string offerId) => Path.Combine(_baseDir, $"{offerId}.json");

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        if (normA == 0 || normB == 0) return 0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
