namespace Midango.Api.Venues;

/// M2 keeps only what staff accounts need. M3 adds location, opening hours and courts.
public sealed class Venue
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string NameEn { get; set; }
    public required string NameAr { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
