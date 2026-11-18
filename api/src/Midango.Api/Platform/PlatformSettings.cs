namespace Midango.Api.Platform;

/// Marketplace-wide rules. Exactly one row (Id = 1), seeded by the migration.
public sealed class PlatformSettings
{
    public const int SingletonId = 1;
    public int Id { get; init; } = SingletonId;

    /// "Still coming?" goes out this many hours before the start.
    public int ReminderHoursBefore { get; set; } = 3;

    /// This many no-shows within NoShowWindowDays limits the customer…
    public int NoShowLimit { get; set; } = 2;
    public int NoShowWindowDays { get; set; } = 30;

    /// …to this many upcoming bookings at a time.
    public int LimitedUpcomingBookings { get; set; } = 1;

    // Loyalty. Points are earned on cash paid at check-in and spent as money off a booking.
    public decimal PointsPerSar { get; set; } = 1;

    /// Checked-in games within TierWindowDays to reach each tier, and the tier's earn multiplier. Bronze = ×1.
    public int SilverGames { get; set; } = 10;
    public int GoldGames { get; set; } = 25;
    public decimal SilverMultiplier { get; set; } = 1.25m;
    public decimal GoldMultiplier { get; set; } = 1.5m;
    public int TierWindowDays { get; set; } = 90;

    /// Points are spent in steps: PointsPerRedeemStep points = SarPerRedeemStep off, up to MaxRedeemPercent of the amount.
    public int PointsPerRedeemStep { get; set; } = 100;
    public decimal SarPerRedeemStep { get; set; } = 10;
    public int MaxRedeemPercent { get; set; } = 50;
}
