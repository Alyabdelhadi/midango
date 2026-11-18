namespace Midango.Api.Venues;

/// One of a venue's photos (at most 10), shown in SortOrder. Path is the public "/media/…" path.
public sealed class VenuePhoto
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid VenueId { get; init; }
    public required string Path { get; init; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
