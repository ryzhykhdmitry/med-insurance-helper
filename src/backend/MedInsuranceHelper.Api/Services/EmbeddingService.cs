using Azure.AI.OpenAI;
using MedInsuranceHelper.Api.Configuration;
using MedInsuranceHelper.Api.Models;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

namespace MedInsuranceHelper.Api.Services;

/// <summary>Wraps Azure AI Foundry embeddings API calls.</summary>
public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}

/// <summary>
/// Calls the Azure AI Foundry embeddings endpoint to produce vector representations.
/// Supports single and batch embedding with basic error handling.
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _client;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly AppSettings _settings;

    public EmbeddingService(IOptions<AppSettings> options, ILogger<EmbeddingService> logger)
    {
        _logger = logger;
        _settings = options.Value;

        // Specify API version explicitly to ensure compatibility
        var clientOptions = new AzureOpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(30)
        };

        var azureClient = new AzureOpenAIClient(
            new Uri(_settings.FoundryEndpoint),
            new System.ClientModel.ApiKeyCredential(_settings.FoundryApiKey),
            clientOptions);
        _client = azureClient.GetEmbeddingClient(_settings.EmbeddingDeployment);
    }

    /// <inheritdoc/>
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        try
        {
            var result = await _client.GenerateEmbeddingAsync(text, cancellationToken: ct);
            return result.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text of length {Len}.", text.Length);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return Array.Empty<float[]>();

        try
        {
            var results = await _client.GenerateEmbeddingsAsync(texts, cancellationToken: ct);
            return results.Value.Select(e => e.ToFloats().ToArray()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate batch embeddings for {Count} texts.", texts.Count);
            throw;
        }
    }
}
