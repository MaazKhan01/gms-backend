using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services;

public interface IBlobService
{
    Task<string> UploadBase64Async(string base64Content, string fileName, string containerName = null, CancellationToken ct = default);
    string GenerateSasUrl(string blobUrl, int expiryMinutes = 120);
}
