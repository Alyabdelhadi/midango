namespace Midango.Api.Platform;

/// A venue that asked for a demo from the website. Read by the platform operator.
public sealed class VenueLead
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string VenueName { get; init; }
    public required string ContactName { get; init; }
    public required string Phone { get; init; }
    public required string City { get; init; }
    public int Courts { get; init; }
    public string? Message { get; init; }
    /// "en" or "ar": the page the request came from.
    public required string Language { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ContactedAt { get; set; }
}
