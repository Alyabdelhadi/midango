using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Bookings;

/// The manager's look back: how full the courts were, the cash taken and how many people didn't show up.
public static class ReportEndpoints
{
    public const int MaxDays = 92;
    private const int Step = 15;

    public sealed record HeatCellDto(DayOfWeek Day, int Hour, int Percent, int BookedMinutes, int OpenMinutes);

    public sealed record CashRowDto(DateOnly WeekStart, Guid CourtId, string CourtNameEn, string CourtNameAr, decimal Cash, int CheckIns);

    public sealed record ReportDto(
        DateOnly From, DateOnly To, string Currency, int DayStartHour,
        int Bookings, int OccupancyPercent, decimal CashCollected, int CheckIns,
        int Played, int NoShows, int NoShowRatePercent, int LateCancellations, int VenueCancellations,
        IReadOnlyList<HeatCellDto> Heatmap, IReadOnlyList<CashRowDto> Cash);

    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/venue/reports", async (
            DateOnly? from, DateOnly? to, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);
            var zone = VenueClock.Zone(venue);
            var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venue.Id).ToListAsync(ct);
            var now = clock.GetUtcNow();
            var last = to ?? Slots.BusinessDay(VenueClock.ToLocal(now, zone), hours);
            var first = from ?? last.AddDays(-27);
            if (last < first || last.DayNumber - first.DayNumber >= MaxDays)
                return ApiErrors.Fail(400, $"Choose a period of up to {MaxDays} days.");

            var windowFrom = VenueClock.ToInstant(Slots.DayWindow(first, hours).From, zone).ToUniversalTime();
            var windowTo = VenueClock.ToInstant(Slots.DayWindow(last, hours).To, zone).ToUniversalTime();
            var rows = await db.Bookings.AsNoTracking()
                .Where(b => b.VenueId == venue.Id && b.Kind == BookingKind.Booking
                            && b.StartsAt >= windowFrom && b.StartsAt < windowTo)
                .Select(b => new { b.CourtId, b.StartsAt, b.EndsAt, b.Status, b.CheckedInAt, b.CashCollected, b.LateCancellation, b.CancelledByVenue })
                .ToListAsync(ct);
            var courts = await db.Courts.AsNoTracking().Where(c => c.VenueId == venue.Id)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn)
                .Select(c => new { c.Id, c.NameEn, c.NameAr, c.Active }).ToListAsync(ct);
            var activeCourts = courts.Count(c => c.Active);

            // Court-minutes per weekday and wall-clock hour, counted in 15-minute steps.
            var open = new int[7, 24];
            var booked = new int[7, 24];
            for (var day = first; day <= last; day = day.AddDays(1))
            {
                if (Slots.Period(day, hours) is not { } p) continue;
                for (var t = p.Opens; t < p.Closes; t = t.AddMinutes(Step)) open[(int)t.DayOfWeek, t.Hour] += Step * activeCourts;
            }
            var confirmed = rows.Where(r => r.Status == BookingStatus.Confirmed).ToList();
            foreach (var b in confirmed)
            {
                var end = VenueClock.ToLocal(b.EndsAt, zone);
                for (var t = VenueClock.ToLocal(b.StartsAt, zone); t < end; t = t.AddMinutes(Step)) booked[(int)t.DayOfWeek, t.Hour] += Step;
            }

            var heatmap = new List<HeatCellDto>();
            int openTotal = 0, bookedTotal = 0;
            for (var d = 0; d < 7; d++)
            for (var h = 0; h < 24; h++)
            {
                if (open[d, h] == 0) continue;
                openTotal += open[d, h];
                bookedTotal += booked[d, h];
                heatmap.Add(new HeatCellDto((DayOfWeek)d, h, Percent(booked[d, h], open[d, h]), booked[d, h], open[d, h]));
            }

            // Weeks start on Sunday; a booking belongs to its business day (00:30 is still last night).
            var checkedIn = rows.Where(r => r.CheckedInAt is not null).ToList();
            var cash = checkedIn
                .GroupBy(r =>
                {
                    var day = Slots.BusinessDay(VenueClock.ToLocal(r.StartsAt, zone), hours);
                    return (WeekStart: day.AddDays(-(int)day.DayOfWeek), r.CourtId);
                })
                .Select(g =>
                {
                    var court = courts.First(c => c.Id == g.Key.CourtId);
                    return new CashRowDto(g.Key.WeekStart, court.Id, court.NameEn, court.NameAr, g.Sum(r => r.CashCollected ?? 0), g.Count());
                })
                .OrderBy(r => r.WeekStart).ThenBy(r => courts.FindIndex(c => c.Id == r.CourtId))
                .ToList();

            var played = confirmed.Count(r => r.EndsAt <= now);
            var noShows = rows.Count(r => r.Status == BookingStatus.NoShow);
            return Results.Ok(new ReportDto(
                first, last, venue.CurrencyCode, hours.Count == 0 ? 0 : hours.Min(h => h.Opens.Hour),
                confirmed.Count, Percent(bookedTotal, openTotal), checkedIn.Sum(r => r.CashCollected ?? 0), checkedIn.Count,
                played, noShows, Percent(noShows, played + noShows),
                rows.Count(r => r.Status == BookingStatus.Cancelled && r.LateCancellation),
                rows.Count(r => r.Status == BookingStatus.Cancelled && r.CancelledByVenue),
                heatmap, cash));
        }).RequireAuthorization(Policies.VenueManager);
    }

    private static int Percent(int part, int whole) => whole == 0 ? 0 : Math.Min(100, (int)Math.Round(100.0 * part / whole));
}
