using System.Collections.Concurrent;
using Exparter.Application.Identity;
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
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same major version as docker-compose.yml, so tests exercise the database we run.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6-alpine").Build();

    public RecordingEmailSender Emails { get; } = new();

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Email:Host", "localhost");
        builder.UseSetting("Email:EmailVerificationUrl", "exparter://verify-email?token={token}");
        builder.UseSetting("Email:PasswordResetUrl", "exparter://reset-password?token={token}");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
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
