using MedInsuranceHelper.Api.Configuration;
using MedInsuranceHelper.Api.Models;
using Microsoft.Extensions.Options;

namespace MedInsuranceHelper.Api.Services;

/// <summary>Service that splits document pages into overlapping text chunks.</summary>
public interface IChunkingService
{
    IReadOnlyList<DocumentChunk> Chunk(string offerId, IReadOnlyList<ParsedPage> pages);
}

/// <summary>
/// Sliding-window chunker: produces fixed-size chunks with configurable overlap.
/// Each chunk carries page and character-offset metadata for citation purposes.
/// </summary>
public class ChunkingService : IChunkingService
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    private readonly ILogger<ChunkingService> _logger;

    public ChunkingService(IOptions<AppSettings> options, ILogger<ChunkingService> logger)
    {
        _logger = logger;
        _chunkSize = options.Value.ChunkSize;
        _chunkOverlap = options.Value.ChunkOverlap;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DocumentChunk> Chunk(string offerId, IReadOnlyList<ParsedPage> pages)
    {
        // Build a flat text buffer, tracking page boundaries
        var chunks = new List<DocumentChunk>();
        var pageOffsets = new List<(int Start, int End, int PageNumber)>();
        var fullText = BuildFullText(pages, pageOffsets);

        int globalOffset = 0;
        while (globalOffset < fullText.Length)
        {
            int end = Math.Min(globalOffset + _chunkSize, fullText.Length);
            var chunkText = fullText[globalOffset..end];

            var (startPage, endPage) = GetPageRange(pageOffsets, globalOffset, end - 1);

            chunks.Add(new DocumentChunk
            {
                OfferId = offerId,
                Text = chunkText,
                StartPage = startPage,
                EndPage = endPage,
                Offset = globalOffset,
                Length = chunkText.Length
            });

            // Advance by (chunkSize - overlap) to create sliding window
            int advance = _chunkSize - _chunkOverlap;
            if (advance <= 0) advance = _chunkSize; // guard against misconfiguration
            globalOffset += advance;
        }

        _logger.LogInformation("Chunked offer {OfferId} into {Count} chunks (size={Size}, overlap={Overlap}).",
            offerId, chunks.Count, _chunkSize, _chunkOverlap);
        return chunks;
    }

    private static string BuildFullText(IReadOnlyList<ParsedPage> pages, List<(int Start, int End, int PageNumber)> offsets)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var page in pages)
        {
            int start = sb.Length;
            sb.Append(page.Text);
            sb.Append(' '); // page separator
            offsets.Add((start, sb.Length - 1, page.PageNumber));
        }
        return sb.ToString();
    }

    private static (int StartPage, int EndPage) GetPageRange(
        List<(int Start, int End, int PageNumber)> offsets, int chunkStart, int chunkEnd)
    {
        int startPage = 1, endPage = 1;
        foreach (var (start, end, page) in offsets)
        {
            if (chunkStart >= start && chunkStart <= end) startPage = page;
            if (chunkEnd >= start && chunkEnd <= end) endPage = page;
        }
        return (startPage, endPage);
    }
}
