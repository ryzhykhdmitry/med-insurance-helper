namespace MedInsuranceHelper.Api.Models;

/// <summary>Status of an insurance offer in the ingestion pipeline.</summary>
public enum OfferStatus
{
    Uploaded,
    Processing,
    Processed,
    Failed
}

/// <summary>Represents an insurance offer PDF stored in blob storage.</summary>
public class InsuranceOffer
{
    /// <summary>Unique identifier (UUID).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Name of the insurance company.</summary>
    public string InsurerName { get; set; } = string.Empty;

    /// <summary>Human-readable title of the offer document.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>URI pointing to the PDF in blob storage.</summary>
    public string BlobUri { get; set; } = string.Empty;

    /// <summary>Timestamp when this record was created.</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional version string of the document.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Current processing status.</summary>
    public OfferStatus Status { get; set; } = OfferStatus.Uploaded;
}
