using MedInsuranceHelper.Api.Models;
using MedInsuranceHelper.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedInsuranceHelper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController : ControllerBase
{
    private readonly IOfferRepository _repo;
    private readonly ILogger<IngestController> _logger;

    public IngestController(IOfferRepository repo, ILogger<IngestController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new insurance offer PDF from blob storage.
    /// Returns 202 Accepted with the new offerId so the caller can trigger processing.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Ingest([FromBody] IngestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BlobUri) ||
            string.IsNullOrWhiteSpace(request.InsurerName) ||
            string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("blobUri, insurerName and title are required.");
        }

        var offer = new InsuranceOffer
        {
            InsurerName = request.InsurerName,
            Title = request.Title,
            BlobUri = request.BlobUri,
            Status = OfferStatus.Uploaded
        };

        _repo.Add(offer);
        _logger.LogInformation("Ingested offer {OfferId} ('{Title}' from {Insurer}).", offer.Id, offer.Title, offer.InsurerName);

        return Accepted(new IngestResponse(offer.Id));
    }
}

public record IngestRequest(string BlobUri, string InsurerName, string Title);
public record IngestResponse(string OfferId);
