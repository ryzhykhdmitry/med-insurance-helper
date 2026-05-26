using MedInsuranceHelper.Api.Configuration;
using Microsoft.Extensions.Options;

namespace MedInsuranceHelper.Api.Services;

/// <summary>Compares multiple insurance offers on specified aspects via RAG retrieval.</summary>
public interface IComparisonService
{
    Task<Dictionary<string, Dictionary<string, string?>>> CompareAsync(
        IReadOnlyList<string> offerIds,
        IReadOnlyList<string> aspects,
        CancellationToken ct = default);
}

/// <summary>
/// For each (aspect, offerId) pair, runs RetrievalService and picks the top snippet.
/// Missing results are represented as null (explicit null marker).
/// </summary>
public class ComparisonService : IComparisonService
{
    private readonly IRetrievalService _retrieval;
    private readonly int _defaultTopK;
    private readonly ILogger<ComparisonService> _logger;

    public ComparisonService(
        IRetrievalService retrieval,
        IOptions<AppSettings> options,
        ILogger<ComparisonService> logger)
    {
        _retrieval = retrieval;
        _defaultTopK = options.Value.DefaultTopK;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, Dictionary<string, string?>>> CompareAsync(
        IReadOnlyList<string> offerIds,
        IReadOnlyList<string> aspects,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, Dictionary<string, string?>>(StringComparer.OrdinalIgnoreCase);

        foreach (var aspect in aspects)
        {
            var byOffer = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var offerId in offerIds)
            {
                ct.ThrowIfCancellationRequested();
                var query = $"{aspect} coverage in this insurance plan";

                var chunks = await _retrieval.RetrieveAsync(query, topK: 1, filterOfferId: offerId, ct: ct);
                byOffer[offerId] = chunks.Count > 0
                    ? (chunks[0].Text.Length > 300 ? chunks[0].Text[..300] + "…" : chunks[0].Text)
                    : null; // explicit null = not found in this offer

                _logger.LogDebug("Aspect '{Aspect}' for offer '{OfferId}': {Found}.",
                    aspect, offerId, byOffer[offerId] != null ? "found" : "not found");
            }

            result[aspect] = byOffer;
        }

        return result;
    }
}
