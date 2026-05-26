using MedInsuranceHelper.Api.Services;
using MedInsuranceHelper.Api.Services.VectorStore;
using Microsoft.AspNetCore.Mvc;

namespace MedInsuranceHelper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IEmbeddingService _embedder;
    private readonly IFileVectorStore _vectorStore;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        IEmbeddingService embedder,
        IFileVectorStore vectorStore,
        ILogger<SearchController> logger)
    {
        _embedder = embedder;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    /// <summary>
    /// Embeds the query and retrieves the top-K most relevant document chunks.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] SearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("query is required.");

        int topK = request.TopK > 0 ? request.TopK : 5;
        var queryEmbedding = await _embedder.EmbedAsync(request.Query, ct);
        var results = await _vectorStore.SearchAsync(queryEmbedding, topK, ct: ct);

        var response = new SearchResponse(
            results.Select(r => new SearchResult(r.OfferId, r.Text, r.StartPage, r.Score)).ToList());

        _logger.LogInformation("Search for '{Query}' returned {Count} results.", request.Query, results.Count);
        return Ok(response);
    }
}

public record SearchRequest(string Query, int TopK = 5);
public record SearchResult(string OfferId, string Snippet, int Page, double Score);
public record SearchResponse(IReadOnlyList<SearchResult> Results);
