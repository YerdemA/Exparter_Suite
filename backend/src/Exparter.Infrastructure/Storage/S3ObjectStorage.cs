using System.ComponentModel.DataAnnotations;
using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Exparter.Application.Common;
using Microsoft.Extensions.Options;

namespace Exparter.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Address the API itself uses to reach the storage service.</summary>
    [Required]
    [Url]
    public string Endpoint { get; init; } = null!;

    /// <summary>
    /// Address clients use, written into presigned URLs. Differs from <see cref="Endpoint"/>
    /// whenever clients reach the storage through another host name than the API does.
    /// Defaults to <see cref="Endpoint"/>.
    /// </summary>
    [Url]
    public string? PublicEndpoint { get; init; }

    [Required]
    public string Region { get; init; } = null!;

    [Required]
    public string Bucket { get; init; } = null!;

    [Required]
    public string AccessKey { get; init; } = null!;

    [Required]
    public string SecretKey { get; init; } = null!;
}

/// <summary>Works with any S3-compatible service (see ADR 0010).</summary>
internal sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly StorageOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly AmazonS3Client _client;

    // A presigned URL is signed for one host, so URLs handed to clients must be created
    // by a client configured with the address those clients will call.
    private readonly AmazonS3Client _presigner;
    private readonly bool _publicEndpointIsHttp;

    public S3ObjectStorage(IOptions<StorageOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        var publicEndpoint = _options.PublicEndpoint ?? _options.Endpoint;
        _client = CreateClient(_options.Endpoint);
        _presigner = CreateClient(publicEndpoint);
        _publicEndpointIsHttp = publicEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    public PresignedUpload CreateUploadUrl(string key, string contentType, TimeSpan lifetime)
    {
        var expiresAt = _timeProvider.GetUtcNow() + lifetime;
        var url = _presigner.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = expiresAt.UtcDateTime,
            Protocol = Protocol,
        });
        return new PresignedUpload(new Uri(url), expiresAt);
    }

    public Uri CreateDownloadUrl(string key, TimeSpan lifetime) =>
        new(_presigner.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = (_timeProvider.GetUtcNow() + lifetime).UtcDateTime,
            Protocol = Protocol,
        }));

    public async Task<StoredObjectInfo?> GetInfoAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await _client.GetObjectMetadataAsync(_options.Bucket, key, cancellationToken);
            return new StoredObjectInfo(metadata.ContentLength, metadata.Headers.ContentType);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        _client.DeleteObjectAsync(_options.Bucket, key, cancellationToken);

    public void Dispose()
    {
        _client.Dispose();
        _presigner.Dispose();
    }

    // The SDK presigns with https unless told otherwise, whatever the service URL says.
    private Protocol Protocol => _publicEndpointIsHttp ? Protocol.HTTP : Protocol.HTTPS;

    private AmazonS3Client CreateClient(string endpoint) =>
        new(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                AuthenticationRegion = _options.Region,

                // Self-hosted services are addressed as host/bucket/key, not bucket.host/key.
                ForcePathStyle = true,
            });
}
