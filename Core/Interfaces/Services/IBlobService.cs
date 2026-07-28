using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services;

public interface IBlobService
{
    /// <summary>Storage account host, e.g. "eventful.blob.core.windows.net".
    /// Used by BlobSasMiddleware to spot blob URLs in outgoing JSON.</summary>
    string BlobHost { get; }

    Task<string> UploadBase64Async(string base64Content, string fileName, string containerName = null, CancellationToken ct = default);
    string GenerateSasUrl(string blobUrl, int expiryMinutes = 120);
}
