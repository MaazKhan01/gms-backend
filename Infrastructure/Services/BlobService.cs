using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Interfaces.Services;

namespace Infrastructure.Services;

public class BlobService : IBlobService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _defaultContainer;
    private readonly ILogger<BlobService> _logger;

    public BlobService(BlobServiceClient blobServiceClient, IConfiguration configuration, ILogger<BlobService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _defaultContainer = configuration["AzureStorage:BlobContainerName"] ?? "media";
        _logger = logger;
    }

    public async Task<string> UploadBase64Async(string base64Content, string fileName, string containerName = null, CancellationToken ct = default)
    {
        var container = containerName ?? _defaultContainer;
        var containerClient = _blobServiceClient.GetBlobContainerClient(container);

        var ext = Path.GetExtension(fileName);
        var blobName = $"{Guid.NewGuid()}{ext}";
        var blobClient = containerClient.GetBlobClient(blobName);

        var bytes = Convert.FromBase64String(base64Content);
        var contentType = GetContentType(ext);

        using var stream = new MemoryStream(bytes);
        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);

        _logger.LogInformation("Uploaded blob {BlobName} to container {Container}", blobName, container);
        return blobClient.Uri.ToString();
    }

    public string GenerateSasUrl(string blobUrl, int expiryMinutes = 120)
    {
        if (string.IsNullOrWhiteSpace(blobUrl)) return blobUrl;

        var uriBuilder = new BlobUriBuilder(new Uri(blobUrl));
        var blobClient = _blobServiceClient
            .GetBlobContainerClient(uriBuilder.BlobContainerName)
            .GetBlobClient(uriBuilder.BlobName);

        return blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)).ToString();
    }

    private static string GetContentType(string ext) => ext?.ToLower() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
}
