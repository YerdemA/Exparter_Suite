using System.Collections.Concurrent;
using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Notifications;
using Exparter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Exparter.Api.IntegrationTests;

/// <summary>
/// Runs the real API against a throwaway PostgreSQL container. Shared by all tests, so
/// each test must create its own users instead of assuming an empty database.
/// </summary>
public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same major version as docker-compose.yml, so tests exercise the database we run.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6-alpine").Build();

    public RecordingEmailSender Emails { get; } = new();

    public InMemoryObjectStorage Storage { get; } = new();

    public RecordingPushSender Pushes { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>There is no endpoint for granting the admin role; an operator does it in the database.</summary>
    public async Task MakeAdminAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE identity.users SET role = 'Admin' WHERE id = {userId}");
    }

    /// <summary>
    /// Every test client shares one address and many users are created per test, so the
    /// limits are out of the way here; <see cref="StrictRateLimitFixture"/> tests them.
    /// </summary>
    protected virtual void ConfigureRateLimits(IWebHostBuilder builder)
    {
        builder.UseSetting("RateLimits:UserPerMinute", "1000000");
        builder.UseSetting("RateLimits:AnonymousPerMinute", "1000000");
        builder.UseSetting("RateLimits:CredentialsPerMinute", "1000000");
        builder.UseSetting("RateLimits:EmailsPerQuarterHour", "1000000");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Email:Host", "localhost");
        builder.UseSetting("Storage:Endpoint", "http://localhost:3900");
        builder.UseSetting("Storage:AccessKey", "unused");
        builder.UseSetting("Storage:SecretKey", "unused");
        builder.UseSetting("Email:EmailVerificationUrl", "exparter://app/verify-email?token={token}");
        builder.UseSetting("Email:PasswordResetUrl", "exparter://app/reset-password?token={token}");
        ConfigureRateLimits(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);

            // The real S3 client is covered by S3ObjectStorageTests; API tests only need
            // to control which files "exist".
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<IObjectStorage>(Storage);

            services.RemoveAll<IPushSender>();
            services.AddSingleton<IPushSender>(Pushes);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}

public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, StoredObjectInfo> _objects = new();

    /// <summary>Stands in for a client uploading a file to the presigned URL.</summary>
    public void Upload(string key, long contentLength, string contentType) =>
        _objects[key] = new StoredObjectInfo(contentLength, contentType);

    public bool Contains(string key) => _objects.ContainsKey(key);

    public PresignedUpload CreateUploadUrl(string key, string contentType, TimeSpan lifetime) =>
        new(new Uri($"https://storage.test/upload/{key}"), DateTimeOffset.UtcNow + lifetime);

    public Uri CreateDownloadUrl(string key, TimeSpan lifetime) => new($"https://storage.test/download/{key}");

    public Task<StoredObjectInfo?> GetInfoAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(_objects.GetValueOrDefault(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

/// <summary>Stands in for FCM: records what each device token was sent.</summary>
public sealed class RecordingPushSender : IPushSender
{
    private readonly ConcurrentQueue<(string Token, PushMessage Message)> _sent = new();

    /// <summary>Tokens that FCM answers as no longer registered.</summary>
    public ConcurrentDictionary<string, bool> Stale { get; } = new();

    public Task<IReadOnlyCollection<string>> SendAsync(
        IReadOnlyCollection<string> tokens,
        PushMessage message,
        CancellationToken cancellationToken)
    {
        foreach (var token in tokens)
        {
            _sent.Enqueue((token, message));
        }

        return Task.FromResult<IReadOnlyCollection<string>>(tokens.Where(Stale.ContainsKey).ToList());
    }

    public IReadOnlyList<PushMessage> SentTo(string token) =>
        _sent.Where(x => x.Token == token).Select(x => x.Message).ToList();

    /// <summary>Pushes are sent in the background; waits until the token has received <paramref name="count"/> of them.</summary>
    public async Task<IReadOnlyList<PushMessage>> WaitForAsync(string token, int count)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (SentTo(token).Count < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }

        return SentTo(token);
    }
}

public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, string> _verificationTokens = new();
    private readonly ConcurrentDictionary<string, string> _passwordResetTokens = new();

    public Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken)
    {
        _verificationTokens[email] = token;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken)
    {
        _passwordResetTokens[email] = token;
        return Task.CompletedTask;
    }

    public string LatestVerificationToken(string email) => _verificationTokens[email];

    public string? LatestPasswordResetToken(string email) => _passwordResetTokens.GetValueOrDefault(email);
}
