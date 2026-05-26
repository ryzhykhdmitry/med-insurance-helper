using MedInsuranceHelper.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedInsuranceHelper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompareController : ControllerBase
{
    private readonly IComparisonService _comparison;
    private readonly ILogger<CompareController> _logger;

    public CompareController(IComparisonService comparison, ILogger<CompareController> logger)
    {
        _comparison = comparison;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/compare — compares multiple offers across specified aspects.
    /// Returns a structured table: { aspect → { offerId → snippet } }.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CompareResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Compare([FromBody] CompareRequest request, CancellationToken ct)
    {
        if (request.OfferIds == null || request.OfferIds.Count < 2)
            return BadRequest("At least two offerIds are required.");
        if (request.Aspects == null || request.Aspects.Count == 0)
            return BadRequest("At least one aspect is required.");

        var result = await _comparison.CompareAsync(request.OfferIds, request.Aspects, ct);
        _logger.LogInformation("Compared {OfferCount} offers across {AspectCount} aspects.",
            request.OfferIds.Count, request.Aspects.Count);
        return Ok(new CompareResponse(result));
    }
}

public record CompareRequest(IReadOnlyList<string> OfferIds, IReadOnlyList<string> Aspects);

/// <summary>
/// Comparison result: outer key = aspect, inner key = offerId, value = snippet or null if not found.
/// </summary>
public record CompareResponse(Dictionary<string, Dictionary<string, string?>> Comparison);
