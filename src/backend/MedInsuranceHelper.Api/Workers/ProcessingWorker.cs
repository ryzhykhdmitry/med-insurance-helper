using MedInsuranceHelper.Api.Models;
using MedInsuranceHelper.Api.Services;
using MedInsuranceHelper.Api.Services.VectorStore;
using Microsoft.AspNetCore.Mvc;

namespace MedInsuranceHelper.Api.Workers;

/// <summary>
/// Background processing endpoint: downloads PDF from blob, chunks it,
/// generates embeddings, and writes vectors to the local JSON store.
/// </summary>
[ApiController]
[Route("api/process")]
public class ProcessingWorker : ControllerBase
{
    private readonly IOfferRepository _repo;
    private readonly IBlobStorageService _blob;
    private readonly IPdfIngestionService _pdf;
    private readonly IChunkingService _chunker;
    private readonly IEmbeddingService _embedder;
    private readonly IFileVectorStore _vectorStore;
    private readonly ILogger<ProcessingWorker> _logger;

    public ProcessingWorker(
        IOfferRepository repo,
        IBlobStorageService blob,
        IPdfIngestionService pdf,
        IChunkingService chunker,
        IEmbeddingService embedder,
        IFileVectorStore vectorStore,
        ILogger<ProcessingWorker> logger)
    {
        _repo = repo;
        _blob = blob;
        _pdf = pdf;
        _chunker = chunker;
        _embedder = embedder;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    /// <summary>
    /// Triggers the ingestion pipeline for a registered offer.
    /// Downloads PDF → parses pages → chunks → embeds → stores vectors.
    /// </summary>
    [HttpPost("{offerId}")]
    [ProducesResponseType(typeof(ProcessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Process(string offerId, CancellationToken ct)
    {
        var offer = _repo.Get(offerId);
        if (offer is null)
            return NotFound($"Offer '{offerId}' not found.");

        // Kick off in the background — return immediately so callers get 200 fast
        _ = Task.Run(() => RunPipelineAsync(offer, ct), CancellationToken.None);

        return Ok(new ProcessResponse("processing"));
    }

    private async Task RunPipelineAsync(InsuranceOffer offer, CancellationToken ct)
    {
        _logger.LogInformation("Starting pipeline for offer {OfferId}.", offer.Id);
        offer.Status = OfferStatus.Processing;
        _repo.Update(offer);

        try
        {
            // 1. Download PDF
            await using var stream = await _blob.DownloadAsync(offer.BlobUri, ct);

            // 2. Parse pages
            var pages = await _pdf.ParseAsync(stream, offer.Id, ct);
            if (pages.Count == 0)
            {
                NotifyIngestionFailure(offer, "PDF produced no extractable pages.");
                return;
            }

            // 3. Chunk
            var chunks = _chunker.Chunk(offer.Id, pages);

            // 4. Embed — batch for efficiency
            var texts = chunks.Select(c => c.Text).ToList();
            var embeddings = await _embedder.EmbedBatchAsync(texts, ct);

            for (int i = 0; i < chunks.Count; i++)
                chunks[i].Embedding = embeddings[i];

            // 5. Store vectors
            await _vectorStore.UpsertAsync(offer.Id, chunks, ct);

            offer.Status = OfferStatus.Processed;
            _repo.Update(offer);
            _logger.LogInformation("Pipeline complete for offer {OfferId}. {ChunkCount} chunks embedded.", offer.Id, chunks.Count);
        }
        catch (Exception ex)
        {
            NotifyIngestionFailure(offer, ex.Message);
        }
    }

    /// <summary>
    /// Logs a structured failure notification so operators can identify unreadable PDFs.
    /// T040: ingestion-failure notification.
    /// </summary>
    private void NotifyIngestionFailure(InsuranceOffer offer, string reason)
    {
        offer.Status = OfferStatus.Failed;
        _repo.Update(offer);
        _logger.LogError(
            "INGESTION_FAILURE: OfferId={OfferId} | Insurer={Insurer} | Title={Title} | Reason={Reason}. " +
            "Operator action required: verify the PDF is readable and re-trigger /api/process/{OfferId}.",
            offer.Id, offer.InsurerName, offer.Title, reason, offer.Id);
    }
}

public record ProcessResponse(string Status);
