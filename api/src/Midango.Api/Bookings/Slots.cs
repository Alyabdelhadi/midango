using Midango.Api.Catalog;
using Midango.Api.Venues;

namespace Midango.Api.Bookings;

/// Converts between UTC instants and the venue's wall clock.
public static class VenueClock
{
    public static TimeZoneInfo Zone(Venue venue) => TimeZoneInfo.FindSystemTimeZoneById(venue.TimeZoneId);

    /// ponytail: takes the zone's offset at that wall-clock time. Correct for Asia/Riyadh (no DST);
    /// ambiguous/skipped times in DST zones need handling before such a venue launches.
    public static DateTimeOffset ToInstant(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified));
    }

    public static DateTime ToLocal(DateTimeOffset instant, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(instant, zone).DateTime;
}

/// Start times a venue sells, from its weekly opening hours. All values are venue-local wall-clock times.
public static class Slots
{
    public const int StepMinutes = 30;
    public const int MaxDaysAhead = 30;

    /// The opening period of one business day; it ends on the next calendar day when it closes after midnight.
    public static (DateTime Opens, DateTime Closes)? Period(DateOnly day, IReadOnlyList<OpeningHours> hours)
    {
        var row = hours.FirstOrDefault(h => h.Day == day.DayOfWeek);
        if (row is null) return null;
        var opens = day.ToDateTime(row.Opens);
        var closes = day.ToDateTime(row.Closes);
        if (row.Closes <= row.Opens) closes = closes.AddDays(1);
        return (opens, closes);
    }

    /// Every StepMinutes from opening while the whole booking fits before closing.
    public static IEnumerable<DateTime> Starts(DateOnly day, IReadOnlyList<OpeningHours> hours, int minutes)
    {
        if (Period(day, hours) is not { } period) yield break;
        for (var start = period.Opens; start.AddMinutes(minutes) <= period.Closes; start = start.AddMinutes(StepMinutes))
            yield return start;
    }

    /// True when the booking is one of the sold start times of its own business day or of the previous one
    /// (a 00:30 start belongs to the evening before when that day closes after midnight).
    public static bool Bookable(DateTime localStart, int minutes, IReadOnlyList<OpeningHours> hours)
    {
        if (localStart.Second != 0 || localStart.Millisecond != 0) return false;
        var day = DateOnly.FromDateTime(localStart);
        return Fits(day) || Fits(day.AddDays(-1));

        bool Fits(DateOnly businessDay) =>
            Period(businessDay, hours) is { } p
            && localStart >= p.Opens
            && localStart.AddMinutes(minutes) <= p.Closes
            && (localStart - p.Opens).TotalMinutes % StepMinutes == 0;
    }
}
