namespace Midango.Api.Venues;

public sealed class Venue
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string NameEn { get; set; }
    public required string NameAr { get; set; }
    public string? AddressEn { get; set; }
    public string? AddressAr { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// IANA id. Slot times (M5) are calculated in the venue's own time zone.
    public string TimeZoneId { get; set; } = "Asia/Riyadh";

    /// Visible in the app. Only allowed when nothing is missing (see VenueEndpoints.MissingForPublish).
    public bool Published { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
