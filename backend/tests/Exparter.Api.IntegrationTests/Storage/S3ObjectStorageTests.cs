using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Exparter.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Exparter.Api.IntegrationTests.Storage;

/// <summary>
/// Exercises the real S3 client against the same storage service and version that
/// docker-compose.yml runs, including uploads and downloads through presigned URLs.
/// </summary>
public sealed class S3ObjectStorageTests : IAsyncLifetime
{
    private const string AccessKey = "GK0123456789abcdef01234567";
    private const string SecretKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Bucket = "exparter-test";

    private const string Config =
        """
        metadata_dir = "/var/lib/garage/meta"
        data_dir = "/var/lib/garage/data"
        db_engine = "sqlite"
        replication_factor = 1
        rpc_bind_addr = "[::]:3901"
        rpc_secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"

        [s3_api]
        s3_region = "garage"
        api_bind_addr = "[::]:3900"
        """;

    private readonly IContainer _garage = new ContainerBuilder("dxflrs/garage:v2.4.1")
        .WithCommand("/garage", "server", "--single-node", "--default-bucket")
        .WithEnvironment("GARAGE_DEFAULT_ACCESS_KEY", AccessKey)
        .WithEnvironment("GARAGE_DEFAULT_SECRET_KEY", SecretKey)
        .WithEnvironment("GARAGE_DEFAULT_BUCKET", Bucket)
        .WithResourceMapping(Encoding.UTF8.GetBytes(Config), "/etc/garage.toml")
        .WithPortBinding(3900, assignRandomHostPort: true)

        // The image has no shell, so readiness is probed from outside: an unsigned
        // request is refused with 403 once the S3 API is up.
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
            .ForPort(3900)
            .ForStatusCode(HttpStatusCode.Forbidden)))
        .Build();

    private readonly HttpClient _http = new();
    private S3ObjectStorage _storage = null!;

    public async Task InitializeAsync()
    {
        await _garage.StartAsync();
        _storage = new S3ObjectStorage(
            Options.Create(new StorageOptions
            {
                Endpoint = $"http://{_garage.Hostname}:{_garage.GetMappedPublicPort(3900)}",
                Region = "garage",
                Bucket = Bucket,
                AccessKey = AccessKey,
                SecretKey = SecretKey,
            }),
            TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        _storage.Dispose();
        _http.Dispose();
        await _garage.DisposeAsync();
    }

    private async Task<HttpResponseMessage> PutAsync(Uri url, byte[] content, string contentType)
    {
        using var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return await _http.PutAsync(url, body);
    }

    [Fact]
    public async Task A_file_uploaded_through_a_presigned_url_can_be_inspected_downloaded_and_deleted()
    {
        const string key = "avatars/test/photo.png";
        var content = Encoding.UTF8.GetBytes("not really a png");

        var upload = _storage.CreateUploadUrl(key, "image/png", TimeSpan.FromMinutes(5));
        var put = await PutAsync(upload.Url, content, "image/png");
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());

        var info = await _storage.GetInfoAsync(key, CancellationToken.None);
        Assert.NotNull(info);
        Assert.Equal(content.Length, info.ContentLength);
        Assert.Equal("image/png", info.ContentType);

        var downloaded = await _http.GetByteArrayAsync(_storage.CreateDownloadUrl(key, TimeSpan.FromMinutes(5)));
        Assert.Equal(content, downloaded);

        await _storage.DeleteAsync(key, CancellationToken.None);
        Assert.Null(await _storage.GetInfoAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task GetInfo_returns_null_for_a_missing_object() =>
        Assert.Null(await _storage.GetInfoAsync("avatars/test/missing.jpg", CancellationToken.None));

    [Fact]
    public async Task A_presigned_upload_url_does_not_accept_another_content_type()
    {
        var upload = _storage.CreateUploadUrl("avatars/test/wrong-type.png", "image/png", TimeSpan.FromMinutes(5));

        var put = await PutAsync(upload.Url, [1, 2, 3], "text/html");

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task An_object_cannot_be_read_without_a_signed_url()
    {
        const string key = "avatars/test/private.png";
        var upload = _storage.CreateUploadUrl(key, "image/png", TimeSpan.FromMinutes(5));
        await PutAsync(upload.Url, [1, 2, 3], "image/png");

        var unsigned = new UriBuilder(_storage.CreateDownloadUrl(key, TimeSpan.FromMinutes(5))) { Query = string.Empty }.Uri;
        var response = await _http.GetAsync(unsigned);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
