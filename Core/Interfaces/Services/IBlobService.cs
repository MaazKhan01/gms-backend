using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services;

public interface IBlobService
{
    /// <summary>Storage account host, e.g. "eventful.blob.core.windows.net".
    /// Used by BlobSasMiddleware to spot blob URLs in outgoing JSON.</summary>
    string BlobHost { get; }

    Task<string> UploadBase64Async(string base64Content, string fileName, string containerName = null, CancellationToken ct = default);

    // Raw-stream upload — for files a background job needs to re-read later
    // (bulk-import source files), where the caller never has base64 anyway.
    Task<string> UploadStreamAsync(Stream content, string fileName, string containerName = null, CancellationToken ct = default);

    // Re-opens a blob previously returned by either upload method, by its
    // bare URL (no SAS token needed — this is a server-to-server read using
    // the app's own storage credentials).
    Task<Stream> DownloadAsync(string blobUrl, CancellationToken ct = default);

    string GenerateSasUrl(string blobUrl, int expiryMinutes = 120);
}
