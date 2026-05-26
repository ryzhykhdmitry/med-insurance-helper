using MedInsuranceHelper.Api.Configuration;
using MedInsuranceHelper.Api.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MedInsuranceHelper.Api.Services;

/// <summary>Represents a single parsed page from a PDF document.</summary>
public record ParsedPage(int PageNumber, string Text);

/// <summary>Service responsible for extracting text from PDF documents.</summary>
public interface IPdfIngestionService
{
    Task<IReadOnlyList<ParsedPage>> ParseAsync(Stream pdfStream, string offerId, CancellationToken ct = default);
}

/// <summary>
/// Extracts text page-by-page from a PDF using PdfPig.
/// Includes a heuristic PII detection scan and an OCR fallback stub.
/// </summary>
public class PdfIngestionService : IPdfIngestionService
{
    private readonly ILogger<PdfIngestionService> _logger;
    private readonly AppSettings _settings;

    // PII patterns — deliberately broad for a warning scan; no content is logged.
    private static readonly Regex[] PiiPatterns =
    [
        new(@"\b\d{3}-\d{2}-\d{4}\b"),                          // SSN (US)
        new(@"\b[A-Z]{2}\d{6}[A-Z]\b"),                         // NIN (UK)
        new(@"\bIBAN\s*:?\s*[A-Z]{2}\d{2}[A-Z0-9]{4,30}\b", RegexOptions.IgnoreCase),  // IBAN
        new(@"\b(?:\d[ -]?){13,16}\b"),                         // Credit card
        new(@"\b\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}\b"),         // Date of birth (heuristic)
    ];

    public PdfIngestionService(ILogger<PdfIngestionService> logger, IOptions<AppSettings> options)
    {
        _logger = logger;
        _settings = options.Value;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ParsedPage>> ParseAsync(Stream pdfStream, string offerId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var pages = new List<ParsedPage>();

        try
        {
            using var pdf = PdfDocument.Open(pdfStream);
            foreach (var page in pdf.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                var text = ExtractText(page);

                if (string.IsNullOrWhiteSpace(text))
                {
                    // OCR fallback stub — log and skip; real OCR integration would go here.
                    _logger.LogWarning("Page {Page} of offer {OfferId} has no extractable text. OCR fallback not implemented; page skipped.",
                        page.Number, offerId);
                    continue;
                }

                pages.Add(new ParsedPage(page.Number, text));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse PDF for offer {OfferId}.", offerId);
            throw;
        }

        ScanForPii(pages, offerId);

        _logger.LogInformation("Parsed {PageCount} pages from offer {OfferId}.", pages.Count, offerId);
        return Task.FromResult<IReadOnlyList<ParsedPage>>(pages);
    }

    private static string ExtractText(Page page)
    {
        var sb = new StringBuilder();
        foreach (var word in page.GetWords())
        {
            sb.Append(word.Text).Append(' ');
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Scans extracted text for PII-like patterns and emits a structured WARNING log entry.
    /// No content is included in the log — only the offer ID, page numbers, and match count.
    /// </summary>
    private void ScanForPii(IReadOnlyList<ParsedPage> pages, string offerId)
    {
        var matchSummary = new List<string>();

        foreach (var page in pages)
        {
            foreach (var pattern in PiiPatterns)
            {
                var count = pattern.Matches(page.Text).Count;
                if (count > 0)
                {
                    matchSummary.Add($"Page {page.PageNumber}: pattern '{pattern}' matched {count} time(s)");
                }
            }
        }

        if (matchSummary.Count > 0)
        {
            _logger.LogWarning(
                "PII_DETECTION: OfferId={OfferId} | Potential PII detected in {Count} pattern match(es). " +
                "Review before processing. Summary: [{Summary}]",
                offerId, matchSummary.Count, string.Join("; ", matchSummary));
        }
    }
}
