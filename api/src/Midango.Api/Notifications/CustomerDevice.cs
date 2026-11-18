namespace Midango.Api.Notifications;

/// A phone that can receive push for a customer. The FCM token is unique across customers.
public sealed class CustomerDevice
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; set; }
    public required string Token { get; init; }

    /// "android", "ios" or "web".
    public required string Platform { get; set; }

    /// "en" or "ar": the language reminders are written in.
    public required string Language { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastSeenAt { get; set; }
}
