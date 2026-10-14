using Exparter.Application.Notifications;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exparter.Infrastructure.Notifications;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>
    /// The service account key file of the Firebase project. It is a secret and stays
    /// outside the repository; without it push messages are not sent.
    /// </summary>
    public string? CredentialsPath { get; init; }
}

/// <summary>Sends push messages through Firebase Cloud Messaging.</summary>
internal sealed class FcmPushSender : IPushSender
{
    // FCM accepts at most 500 tokens per multicast request.
    private const int BatchSize = 500;

    private readonly FirebaseMessaging _messaging;

    public FcmPushSender(IOptions<FirebaseOptions> options)
    {
        var credential = CredentialFactory.FromFile<ServiceAccountCredential>(options.Value.CredentialsPath!).ToGoogleCredential();
        _messaging = FirebaseMessaging.GetMessaging(
            FirebaseApp.Create(new AppOptions { Credential = credential }, nameof(FcmPushSender)));
    }

    public async Task<IReadOnlyCollection<string>> SendAsync(
        IReadOnlyCollection<string> tokens,
        PushMessage message,
        CancellationToken cancellationToken)
    {
        var stale = new List<string>();
        foreach (var batch in tokens.Chunk(BatchSize))
        {
            // Registration tokens are deprecated in favour of installation ids, but the
            // Flutter plugin hands out only tokens so far.
#pragma warning disable CS0618
            var response = await _messaging.SendEachForMulticastAsync(
                new MulticastMessage
                {
                    Tokens = batch,
                    Notification = new Notification { Title = message.Title, Body = message.Body },
                    Data = message.Data,
                    Android = new AndroidConfig { Priority = Priority.High },
                },
                cancellationToken);
#pragma warning restore CS0618
            stale.AddRange(batch.Where((_, index) =>
                response.Responses[index].Exception?.MessagingErrorCode
                    is MessagingErrorCode.Unregistered or MessagingErrorCode.SenderIdMismatch));
        }

        return stale;
    }
}

/// <summary>Stands in when no Firebase project is configured, as on CI and in tests.</summary>
internal sealed class DisabledPushSender(ILogger<DisabledPushSender> logger) : IPushSender
{
    public Task<IReadOnlyCollection<string>> SendAsync(
        IReadOnlyCollection<string> tokens,
        PushMessage message,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Push messages are disabled; {Count} devices were not notified", tokens.Count);
        return Task.FromResult<IReadOnlyCollection<string>>([]);
    }
}
