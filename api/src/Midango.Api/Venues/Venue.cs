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

    /// ISO 4217. Prices at this venue are in this currency.
    public string CurrencyCode { get; set; } = "SAR";

    /// Prices are VAT-inclusive; this is the rate used to show the VAT portion.
    public decimal VatPercent { get; set; } = 15m;

    /// Customers cancel for free until this many hours before the start; later counts as a late cancellation.
    public int FreeCancellationHours { get; set; } = 24;

    /// Visible in the app. Only allowed when nothing is missing (see VenueEndpoints.MissingForPublish).
    public bool Published { get; set; }

    /// Set by Midango: suspending unpublishes it, signs its staff out and keeps them out, and blocks publishing.
    public bool Suspended { get; set; }

    /// Public "/media/…" path of the uploaded logo, or null.
    public string? LogoPath { get; set; }

    /// Position in the app's "Featured" strip (1 = first), or null when not featured. Set by Midango.
    public int? FeaturedRank { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// What the venue pays Midango each month. Default uses the platform commission percent.
    public Platform.FeeMode FeeMode { get; set; } = Platform.FeeMode.Default;
    /// Used when FeeMode = Percent: share of cash collected.
    public decimal? FeePercent { get; set; }
    /// Used when FeeMode = Fixed: amount per month, in the venue's currency.
    public decimal? FeeMonthly { get; set; }
}
