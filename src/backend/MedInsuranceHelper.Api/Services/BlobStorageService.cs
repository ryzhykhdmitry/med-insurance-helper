using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MedInsuranceHelper.Api.Configuration;
using Microsoft.Extensions.Options;

namespace MedInsuranceHelper.Api.Services;

/// <summary>Abstraction for blob storage operations.</summary>
public interface IBlobStorageService
{
    Task<string> UploadAsync(string blobName, Stream content, string contentType = "application/pdf", CancellationToken ct = default);
    Task<Stream> DownloadAsync(string blobUri, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListBlobsAsync(CancellationToken ct = default);
    Task EnsureContainerExistsAsync(CancellationToken ct = default);
}

/// <summary>
/// Azure Blob Storage service — compatible with both Azurite (local dev) and Azure Blob Storage (cloud).
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _serviceClient;
    private readonly string _containerName;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IOptions<AppSettings> options, ILogger<BlobStorageService> logger)
    {
        _logger = logger;
        var settings = options.Value;
        _containerName = settings.BlobContainerName;
        _serviceClient = new BlobServiceClient(settings.BlobConnectionString);
    }

    /// <inheritdoc/>
    public async Task EnsureContainerExistsAsync(CancellationToken ct = default)
    {
        var container = _serviceClient.GetBlobContainerClient(_containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        _logger.LogInformation("Blob container '{Container}' is ready.", _containerName);
    }

    /// <inheritdoc/>
    public async Task<string> UploadAsync(string blobName, Stream content, string contentType = "application/pdf", CancellationToken ct = default)
    {
        var container = _serviceClient.GetBlobContainerClient(_containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = container.GetBlobClient(blobName);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };

        await blob.UploadAsync(content, uploadOptions, ct);
        _logger.LogInformation("Uploaded blob '{BlobName}' to container '{Container}'.", blobName, _containerName);
        return blob.Uri.ToString();
    }

    /// <inheritdoc/>
    public async Task<Stream> DownloadAsync(string blobUri, CancellationToken ct = default)
    {
        // Support both full URI and plain blob name
        BlobClient blob;
        if (Uri.TryCreate(blobUri, UriKind.Absolute, out var uri))
        {
            blob = new BlobClient(uri);
            // For Azurite, we need to use the service client to get a properly authenticated client
            var blobName = uri.AbsolutePath.TrimStart('/').Split('/', 2).LastOrDefault() ?? string.Empty;
            var container = _serviceClient.GetBlobContainerClient(_containerName);
            blob = container.GetBlobClient(blobName);
        }
        else
        {
            var container = _serviceClient.GetBlobContainerClient(_containerName);
            blob = container.GetBlobClient(blobUri);
        }

        var response = await blob.DownloadStreamingAsync(cancellationToken: ct);
        _logger.LogInformation("Downloaded blob '{BlobUri}'.", blobUri);
        return response.Value.Content;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListBlobsAsync(CancellationToken ct = default)
    {
        var container = _serviceClient.GetBlobContainerClient(_containerName);
        var blobs = new List<string>();
        await foreach (var item in container.GetBlobsAsync(cancellationToken: ct))
        {
            blobs.Add(item.Name);
        }
        return blobs;
    }
}
