namespace Exparter.Application.Common;

public sealed record PresignedUpload(Uri Url, DateTimeOffset ExpiresAt);

public sealed record StoredObjectInfo(long ContentLength, string? ContentType);

/// <summary>
/// S3-style object storage. Clients upload and download directly with presigned URLs;
/// file contents never pass through the API.
/// </summary>
public interface IObjectStorage
{
    /// <summary>A URL the client can PUT the file to. The upload must send <paramref name="contentType"/>.</summary>
    PresignedUpload CreateUploadUrl(string key, string contentType, TimeSpan lifetime);

    Uri CreateDownloadUrl(string key, TimeSpan lifetime);

    /// <returns>Null if no object exists under <paramref name="key"/>.</returns>
    Task<StoredObjectInfo?> GetInfoAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
