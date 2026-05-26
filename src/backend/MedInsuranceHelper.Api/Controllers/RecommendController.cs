using MedInsuranceHelper.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedInsuranceHelper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecommendController : ControllerBase
{
    private readonly IRecommendationService _recommendation;
    private readonly ILogger<RecommendController> _logger;

    public RecommendController(IRecommendationService recommendation, ILogger<RecommendController> logger)
    {
        _recommendation = recommendation;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/recommend — returns ranked insurance offers matching user criteria.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RecommendResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Recommend([FromBody] RecommendRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Criteria))
            return BadRequest("criteria is required.");

        var results = await _recommendation.RecommendAsync(request.Criteria, ct);
        _logger.LogInformation("Generated {Count} recommendations for criteria '{Criteria}'.",
            results.Count, request.Criteria);
        return Ok(new RecommendResponse(results));
    }
}

public record RecommendRequest(string Criteria);
public record RecommendationItem(string OfferId, string Reason, IReadOnlyList<RecommendCitation> Citations);
public record RecommendCitation(string OfferId, int Page, string Excerpt);
public record RecommendResponse(IReadOnlyList<RecommendationItem> Recommendations);
