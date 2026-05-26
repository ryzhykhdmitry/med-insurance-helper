using MedInsuranceHelper.Api.Configuration;
using MedInsuranceHelper.Api.Controllers;
using MedInsuranceHelper.Api.Services.VectorStore;
using Microsoft.Extensions.Options;

namespace MedInsuranceHelper.Api.Services;

/// <summary>Recommends insurance offers based on free-text user criteria.</summary>
public interface IRecommendationService
{
    Task<IReadOnlyList<RecommendationItem>> RecommendAsync(string criteria, CancellationToken ct = default);
}

/// <summary>
/// Runs RetrievalService for all known offers against the criteria,
/// scores by relevance (mean cosine similarity of top chunks),
/// and returns ranked recommendations with closest-match fallback.
/// </summary>
public class RecommendationService : IRecommendationService
{
    private readonly IRetrievalService _retrieval;
    private readonly IFileVectorStore _vectorStore;
    private readonly IOfferRepository _repo;
    private readonly int _topK;
    private readonly double _minScore;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IRetrievalService retrieval,
        IFileVectorStore vectorStore,
        IOfferRepository repo,
        IOptions<AppSettings> options,
        ILogger<RecommendationService> logger)
    {
        _retrieval = retrieval;
        _vectorStore = vectorStore;
        _repo = repo;
        _topK = options.Value.DefaultTopK;
        _minScore = options.Value.MinRelevanceScore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecommendationItem>> RecommendAsync(string criteria, CancellationToken ct = default)
    {
        var offers = _repo.GetAll();
        if (offers.Count == 0)
        {
            _logger.LogWarning("No insurance offers found in repository for recommendation.");
            return Array.Empty<RecommendationItem>();
        }

        var scored = new List<(string OfferId, double Score, IReadOnlyList<RetrievedChunk> Chunks)>();

        foreach (var offer in offers)
        {
            ct.ThrowIfCancellationRequested();
            var chunks = await _retrieval.RetrieveAsync(criteria, _topK, filterOfferId: offer.Id, ct: ct);
            var score = chunks.Count > 0 ? chunks.Average(c => c.Score) : 0.0;
            scored.Add((offer.Id, score, chunks));
        }

        // Sort by score descending; include fallback (best match even if below threshold)
        var ranked = scored.OrderByDescending(x => x.Score).ToList();
        var results = new List<RecommendationItem>();

        foreach (var (offerId, score, chunks) in ranked)
        {
            if (score <= 0) continue; // skip offers with no vector data at all

            var citations = chunks.Take(3).Select(c => new RecommendCitation(
                c.OfferId, c.StartPage,
                c.Text.Length > 150 ? c.Text[..150] + "…" : c.Text)).ToList();

            var reason = score >= _minScore
                ? $"Relevance score: {score:F2}. This offer closely matches your criteria based on {chunks.Count} relevant passage(s)."
                : $"Closest available match (score: {score:F2}). No perfect match was found; this is the best option from ingested offers.";

            results.Add(new RecommendationItem(offerId, reason, citations));
        }

        _logger.LogInformation("Recommendation complete. {Count} offers scored for criteria '{Criteria}'.",
            results.Count, criteria);
        return results;
    }
}
