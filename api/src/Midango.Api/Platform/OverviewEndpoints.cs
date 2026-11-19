using Microsoft.EntityFrameworkCore;
using Midango.Api.Bookings;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Platform;

/// The operator's look across every venue: what's live, who joined, what got booked and played, and where.
public static class OverviewEndpoints
{
    /// Cash is in the main currency only (the one with the most cash in the period).
    public sealed record DayDto(DateOnly Date, int Bookings, int NewCustomers, int CheckIns, int NoShows, decimal Cash);
    public sealed record MoneyDto(string Currency, decimal Amount);
    public sealed record TopVenueDto(Guid Id, string NameEn, string NameAr, int Bookings, int CheckIns, int NoShows);
    public sealed record SportDto(Guid Id, string NameEn, string NameAr, int Bookings);
    public sealed record RecentDto(
        Guid Id, string Reference, string? CustomerName, string VenueNameEn, string VenueNameAr,
        DateTimeOffset StartsAt, DateTimeOffset CreatedAt, BookingStatus Status, BookingSource Source, bool CheckedIn,
        decimal Total, string Currency);

    /// The same headline numbers for the period before, so the page can show the change.
    public sealed record PreviousDto(int BookingsMade, int NewCustomers, decimal Cash, int NoShowRatePercent);

    /// "Made" counts bookings created in the period; "played" counts bookings whose start fell in it (up to now).
    public sealed record OverviewDto(
        int Days, DateOnly From, DateOnly To, string MainCurrency,
        int VenuesLive, int VenuesDraft, int VenuesSuspended, int CourtsActive,
        int Customers, int CustomersBlocked, int NewCustomers,
        int BookingsMade, int AppBookings, int StaffBookings, int OpenMatches, int Cancelled,
        int Played, int CheckIns, int NoShows, int NoShowRatePercent, IReadOnlyList<MoneyDto> Cash,
        int UpcomingBookings, int LeadsWaiting, PreviousDto Previous,
        IReadOnlyList<DayDto> Series, IReadOnlyList<TopVenueDto> TopVenues, IReadOnlyList<SportDto> BySport,
        IReadOnlyList<RecentDto> Recent);

    private sealed record PlayedRow(Guid VenueId, DateTimeOffset StartsAt, BookingStatus Status, bool CheckedIn, string Currency, decimal Cash);

    public const int MaxDays = 366;

    public static void MapOverviewEndpoints(this IEndpointRouteBuilder app)
    {
        // A preset (?days=7|30|90, ending today) or a custom range (?from=&to=, inclusive, up to MaxDays).
        app.MapGet("/admin/overview", async (
            int? days, DateOnly? from, DateOnly? to, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            // ponytail: days are UTC calendar days across all venues; bucket per venue time zone if operators ask.
            var now = clock.GetUtcNow();
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            DateOnly first, last;
            if (from is { } f && to is { } t)
            {
                if (t < f) return ApiErrors.Fail(400, "The end date must be on or after the start date.");
                if (t.DayNumber - f.DayNumber >= MaxDays) return ApiErrors.Fail(400, $"Choose a range of up to {MaxDays} days.");
                if (f > today) return ApiErrors.Fail(400, "Choose a range that starts today or earlier.");
                (first, last) = (f, t > today ? today : t);
            }
            else
            {
                var preset = days ?? 30;
                if (preset is not (7 or 30 or 90)) return ApiErrors.Fail(400, "Choose 7, 30 or 90 days.");
                (first, last) = (today.AddDays(1 - preset), today);
            }
            var span = last.DayNumber - first.DayNumber + 1;
            var start = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var end = start.AddDays(span);
            // The period before, the same length, for the change on each KPI.
            var prevStart = start.AddDays(-span);

            // ponytail: rows are aggregated in memory; fine at current volume, move to GROUP BY if it grows.
            var made = await db.Bookings.AsNoTracking()
                .Where(b => b.Kind == BookingKind.Booking && b.CreatedAt >= start && b.CreatedAt < end)
                .Select(b => new { b.VenueId, b.CourtId, b.CreatedAt, b.Source, b.Status, b.IsOpenMatch })
                .ToListAsync(ct);
            var played = await Played(db, start, end < now ? end : now, ct);
            var prevPlayed = await Played(db, prevStart, start, ct);
            var joined = await db.Customers.AsNoTracking().Where(c => c.CreatedAt >= start && c.CreatedAt < end).Select(c => c.CreatedAt).ToListAsync(ct);
            var venues = await db.Venues.AsNoTracking().Select(v => new { v.Id, v.NameEn, v.NameAr, v.Published, v.Suspended }).ToListAsync(ct);

            var mainCurrency = played.Concat(prevPlayed).GroupBy(b => b.Currency)
                .OrderByDescending(g => g.Sum(b => b.Cash)).Select(g => g.Key).FirstOrDefault() ?? "SAR";

            static DateOnly Day(DateTimeOffset at) => DateOnly.FromDateTime(at.UtcDateTime);
            var madeByDay = made.CountBy(b => Day(b.CreatedAt)).ToDictionary();
            var joinedByDay = joined.CountBy(Day).ToDictionary();
            var playedByDay = played.ToLookup(b => Day(b.StartsAt));
            var series = Enumerable.Range(0, span).Select(i => first.AddDays(i))
                .Select(d => new DayDto(d, madeByDay.GetValueOrDefault(d), joinedByDay.GetValueOrDefault(d),
                    playedByDay[d].Count(b => b.CheckedIn), playedByDay[d].Count(b => b.Status == BookingStatus.NoShow),
                    playedByDay[d].Where(b => b.Currency == mainCurrency).Sum(b => b.Cash)))
                .ToList();

            var top = made.CountBy(b => b.VenueId).OrderByDescending(g => g.Value).Take(5)
                .Select(g =>
                {
                    var v = venues.First(x => x.Id == g.Key);
                    return new TopVenueDto(v.Id, v.NameEn, v.NameAr, g.Value,
                        played.Count(b => b.VenueId == v.Id && b.CheckedIn),
                        played.Count(b => b.VenueId == v.Id && b.Status == BookingStatus.NoShow));
                })
                .ToList();

            var courtSport = await db.Courts.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.CategoryId, ct);
            var sports = await db.SportCategories.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
            var bySport = made.CountBy(b => courtSport.GetValueOrDefault(b.CourtId))
                .Where(g => sports.ContainsKey(g.Key))
                .OrderByDescending(g => g.Value)
                .Select(g => new SportDto(g.Key, sports[g.Key].NameEn, sports[g.Key].NameAr, g.Value))
                .ToList();

            var recent = await db.Bookings.AsNoTracking()
                .Where(b => b.Kind == BookingKind.Booking)
                .OrderByDescending(b => b.CreatedAt).Take(8)
                .Select(b => new RecentDto(
                    b.Id, b.Reference,
                    b.CustomerId == null ? b.GuestName : db.Customers.Where(c => c.Id == b.CustomerId).Select(c => c.Name).FirstOrDefault(),
                    db.Venues.Where(v => v.Id == b.VenueId).Select(v => v.NameEn).First(),
                    db.Venues.Where(v => v.Id == b.VenueId).Select(v => v.NameAr).First(),
                    b.StartsAt, b.CreatedAt, b.Status, b.Source, b.CheckedInAt != null, b.Total, b.Currency))
                .ToListAsync(ct);

            var prevMade = await db.Bookings.CountAsync(
                b => b.Kind == BookingKind.Booking && b.CreatedAt >= prevStart && b.CreatedAt < start, ct);
            var prevJoined = await db.Customers.CountAsync(c => c.CreatedAt >= prevStart && c.CreatedAt < start, ct);

            return Results.Ok(new OverviewDto(
                span, first, last, mainCurrency,
                venues.Count(v => v.Published && !v.Suspended), venues.Count(v => !v.Published && !v.Suspended),
                venues.Count(v => v.Suspended), await db.Courts.CountAsync(c => c.Active, ct),
                await db.Customers.CountAsync(ct), await db.Customers.CountAsync(c => c.BlockedAt != null, ct), joined.Count,
                made.Count, made.Count(b => b.Source == BookingSource.App), made.Count(b => b.Source == BookingSource.Staff),
                made.Count(b => b.IsOpenMatch), made.Count(b => b.Status == BookingStatus.Cancelled),
                played.Count, played.Count(b => b.CheckedIn), played.Count(b => b.Status == BookingStatus.NoShow), NoShowRate(played),
                played.GroupBy(b => b.Currency).Select(g => new MoneyDto(g.Key, g.Sum(b => b.Cash))).Where(m => m.Amount > 0).ToList(),
                await db.Bookings.CountAsync(b => b.Kind == BookingKind.Booking && b.Status == BookingStatus.Confirmed && b.StartsAt > now, ct),
                await db.VenueLeads.CountAsync(l => l.ContactedAt == null, ct),
                new PreviousDto(prevMade, prevJoined, prevPlayed.Where(b => b.Currency == mainCurrency).Sum(b => b.Cash), NoShowRate(prevPlayed)),
                series, top, bySport, recent));
        }).RequireAuthorization(Policies.PlatformOperator);
    }

    /// Bookings that started in [from, to) and weren't cancelled, with their cash.
    private static Task<List<PlayedRow>> Played(MidangoDb db, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        db.Bookings.AsNoTracking()
            .Where(b => b.Kind == BookingKind.Booking && b.StartsAt >= from && b.StartsAt < to && b.Status != BookingStatus.Cancelled)
            .Select(b => new PlayedRow(
                b.VenueId, b.StartsAt, b.Status, b.CheckedInAt != null, b.Currency,
                // The organiser's cash plus every match player's share, as on the venue report.
                (b.CashCollected ?? 0) + (db.MatchPlayers.Where(p => p.BookingId == b.Id).Sum(p => p.CashCollected) ?? 0)))
            .ToListAsync(ct);

    private static int NoShowRate(List<PlayedRow> rows) =>
        rows.Count == 0 ? 0 : (int)Math.Round(100.0 * rows.Count(b => b.Status == BookingStatus.NoShow) / rows.Count);
}
