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
}
