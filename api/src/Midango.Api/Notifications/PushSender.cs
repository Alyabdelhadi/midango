using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace Midango.Api.Notifications;

public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);

public interface IPushSender
{
    /// Sends one message to each token. Returns the tokens FCM says are no longer valid.
    Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct);
}

/// Used when no Firebase key is configured: the reminder pipeline still runs, pushes go to the log.
public sealed class LogPushSender(ILogger<LogPushSender> log) : IPushSender
{
    public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct)
    {
        foreach (var token in tokens)
            log.LogInformation("PUSH (not sent, no Firebase key) to device …{Tail}: {Title} | {Body}",
                token[^Math.Min(6, token.Length)..], message.Title, message.Body);
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}

public sealed class FcmPushSender : IPushSender
{
    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FcmPushSender> _log;

    public FcmPushSender(string credentialsPath, ILogger<FcmPushSender> log)
    {
        var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
        {
            Credential = CredentialFactory.FromFile<ServiceAccountCredential>(credentialsPath).ToGoogleCredential(),
        });
        _messaging = FirebaseMessaging.GetMessaging(app);
        _log = log;
    }

    public async Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct)
    {
        if (tokens.Count == 0) return [];
        var response = await _messaging.SendEachForMulticastAsync(new MulticastMessage
        {
            // ponytail: FirebaseAdmin 3.7.1 marks Tokens obsolete in favour of Fids (installation IDs), but the app
            // registers FCM registration tokens, which FCM still accepts. Switch when the app sends FIDs.
#pragma warning disable CS0618
            Tokens = tokens.ToList(),
#pragma warning restore CS0618
            Notification = new Notification { Title = message.Title, Body = message.Body },
            Data = message.Data,
        }, ct);

        var dead = new List<string>();
        for (var i = 0; i < response.Responses.Count; i++)
        {
            var error = response.Responses[i].Exception;
            if (error is null) continue;
            if (error.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument) dead.Add(tokens[i]);
            else _log.LogWarning("Push to a device failed: {Code}", error.MessagingErrorCode);
        }
        return dead;
    }
}
