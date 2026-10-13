using System.ComponentModel.DataAnnotations;
using Exparter.Application.Identity;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Exparter.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string Host { get; init; } = null!;

    [Range(1, 65535)]
    public int Port { get; init; } = 1025;

    public string? Username { get; init; }

    public string? Password { get; init; }

    [Required]
    [EmailAddress]
    public string FromAddress { get; init; } = null!;

    public string FromName { get; init; } = "Exparter";

    /// <summary>Link opened from the verification email; <c>{token}</c> is replaced with the token.</summary>
    [Required]
    public string EmailVerificationUrl { get; init; } = null!;

    /// <summary>Link opened from the password reset email; <c>{token}</c> is replaced with the token.</summary>
    [Required]
    public string PasswordResetUrl { get; init; } = null!;
}

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken) =>
        SendAsync(
            email,
            "Exparter e-posta doğrulama",
            $"""
             Merhaba,

             Exparter hesabınızı kullanmaya başlamak için e-posta adresinizi doğrulayın:
             {Link(_options.EmailVerificationUrl, token)}

             Bağlantı 24 saat geçerlidir. Bu hesabı siz oluşturmadıysanız bu e-postayı yok sayabilirsiniz.
             """,
            cancellationToken);

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken) =>
        SendAsync(
            email,
            "Exparter parola sıfırlama",
            $"""
             Merhaba,

             Parolanızı sıfırlamak için aşağıdaki bağlantıyı kullanın:
             {Link(_options.PasswordResetUrl, token)}

             Bağlantı 1 saat geçerlidir. Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz; parolanız değişmez.
             """,
            cancellationToken);

    private static string Link(string template, string token) =>
        template.Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);

    private async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.Auto, cancellationToken);
            if (!string.IsNullOrEmpty(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A mail outage must not fail the request that triggered the email: the data is
            // already saved and the user can ask for a new email.
            logger.LogError(exception, "Sending email with subject {Subject} failed", subject);
        }
    }
}
