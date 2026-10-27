using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Exparter.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Exparter.Api.IntegrationTests.Identity;

/// <summary>
/// Sends the real emails through the same mail catcher that docker-compose.yml runs and
/// reads them back, so both the HTML and the plain-text part are checked as delivered.
/// </summary>
public sealed class SmtpEmailSenderTests : IAsyncLifetime
{
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.31.4")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/livez")))
        .Build();

    public Task InitializeAsync() => _mailpit.StartAsync();

    public Task DisposeAsync() => _mailpit.DisposeAsync().AsTask();

    private SmtpEmailSender Sender() => new(
        Options.Create(new EmailOptions
        {
            Host = _mailpit.Hostname,
            Port = _mailpit.GetMappedPublicPort(1025),
            FromAddress = "no-reply@exparter.app",
            EmailVerificationUrl = "https://exparter.app/verify-email?token={token}",
            PasswordResetUrl = "https://exparter.app/reset-password?token={token}",
        }),
        NullLogger<SmtpEmailSender>.Instance);

    private async Task<JsonElement> OnlyMessageAsync()
    {
        using var http = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}") };
        var list = await http.GetFromJsonAsync<JsonElement>("/api/v1/messages");
        var id = list.GetProperty("messages").EnumerateArray().Single().GetProperty("ID").GetString();
        return await http.GetFromJsonAsync<JsonElement>($"/api/v1/message/{id}");
    }

    [Fact]
    public async Task Verification_email_has_a_branded_html_part_and_a_plain_text_twin()
    {
        await Sender().SendEmailVerificationAsync("elif@example.com", "a+b/c", CancellationToken.None);

        var message = await OnlyMessageAsync();
        var link = "https://exparter.app/verify-email?token=a%2Bb%2Fc";
        Assert.Equal("E-posta adresinizi doğrulayın", message.GetProperty("Subject").GetString());
        var html = message.GetProperty("HTML").GetString()!;
        Assert.Contains($"href=\"{link}\"", html);
        Assert.Contains("E-postamı doğrula", System.Net.WebUtility.HtmlDecode(html));
        Assert.Contains("https://exparter.app/img/email-mark.png", html);
        Assert.Contains($"E-postamı doğrula: {link}", message.GetProperty("Text").GetString());
    }

    [Fact]
    public async Task Password_reset_email_links_to_the_reset_page()
    {
        await Sender().SendPasswordResetAsync("elif@example.com", "token", CancellationToken.None);

        var message = await OnlyMessageAsync();
        Assert.Equal("Parolanızı sıfırlayın", message.GetProperty("Subject").GetString());
        Assert.Contains("href=\"https://exparter.app/reset-password?token=token\"", message.GetProperty("HTML").GetString());
        Assert.Contains("1 saat", message.GetProperty("Text").GetString());
    }
}
