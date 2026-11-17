using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Bookings;

public static class VenueInsightEndpoints
{
    public sealed record VenueCustomerDto(
        Guid Id, string? Name, string PhoneMasked, int Bookings, int Cancellations, DateTimeOffset LastBookingAt);

    public sealed record CustomerLookupDto(Guid Id, string? Name, string PhoneMasked);

    public sealed record TodayDto(
        DateOnly Date, bool Open, DateTimeOffset From, DateTimeOffset To,
        int Bookings, int BookedMinutes, int OccupancyPercent, decimal CashExpected, string Currency, int Cancelled,
        IReadOnlyList<StaffBookingEndpoints.StaffBookingDto> Next);

    public static void MapVenueInsightEndpoints(this IEndpointRouteBuilder app)
    {
        var venueStaff = app.MapGroup("/venue").RequireAuthorization(Policies.VenueStaff);

        venueStaff.MapGet("/customers", async (string? q, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var stats = db.Bookings
                .Where(b => b.VenueId == venueId && b.Kind == BookingKind.Booking && b.CustomerId != null)
                .GroupBy(b => b.CustomerId!.Value)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Bookings = g.Count(b => b.Status == BookingStatus.Confirmed),
                    Cancellations = g.Count(b => b.Status == BookingStatus.Cancelled),
                    Last = g.Max(b => b.StartsAt),
                });
            var rows = from s in stats
                       join c in db.Customers on s.CustomerId equals c.Id
                       select new { c.Id, c.Name, c.PhoneE164, s.Bookings, s.Cancellations, s.Last };

            var term = q?.Trim();
            if (!string.IsNullOrEmpty(term))
            {
                var pattern = $"%{StaffBookingEndpoints.Like(term)}%";
                var phoneTail = term.Length >= 4 && term.All(char.IsAsciiDigit) ? term : null;
                rows = rows.Where(r => (r.Name != null && EF.Functions.ILike(r.Name, pattern))
                                       || (phoneTail != null && r.PhoneE164.EndsWith(phoneTail)));
            }

            var list = await rows.OrderByDescending(r => r.Last).Take(200).AsNoTracking().ToListAsync(ct);
            return Results.Ok(list.Select(r => new VenueCustomerDto(
                r.Id, r.Name, PhoneNumber.Mask(r.PhoneE164), r.Bookings, r.Cancellations, r.Last)));
        });

        venueStaff.MapGet("/customers/lookup", async (string? phone, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            if (!PhoneNumber.TryParse(phone, out var parsed)) return ApiErrors.Fail(400, "Enter a valid mobile number.");
            var venueId = user.StaffVenueId()!.Value;
            var customer = await db.Customers.AsNoTracking()
                .Where(c => c.PhoneE164 == parsed.E164
                            && db.Bookings.Any(b => b.CustomerId == c.Id && b.VenueId == venueId))
                .Select(c => new { c.Id, c.Name, c.PhoneE164 })
                .SingleOrDefaultAsync(ct);
            return customer is null
                ? ApiErrors.Fail(404, "No customer with that number has booked here yet.")
                : Results.Ok(new CustomerLookupDto(customer.Id, customer.Name, PhoneNumber.Mask(customer.PhoneE164)));
        });

        venueStaff.MapGet("/today", async (ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);
            var zone = VenueClock.Zone(venue);
            var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venue.Id).ToListAsync(ct);
            var now = clock.GetUtcNow();
            var day = Slots.BusinessDay(VenueClock.ToLocal(now, zone), hours);
            var period = Slots.Period(day, hours);
            var (fromLocal, toLocal) = Slots.DayWindow(day, hours);
            var from = VenueClock.ToInstant(fromLocal, zone).ToUniversalTime();
            var to = VenueClock.ToInstant(toLocal, zone).ToUniversalTime();

            var inWindow = db.Bookings.Where(b => b.VenueId == venue.Id && b.Kind == BookingKind.Booking
                                                  && b.StartsAt >= from && b.StartsAt < to);
            var confirmed = await inWindow.Where(b => b.Status == BookingStatus.Confirmed)
                .Select(b => new { b.Minutes, b.Total }).ToListAsync(ct);
            var cancelled = await inWindow.CountAsync(b => b.Status == BookingStatus.Cancelled, ct);
            var courts = await db.Courts.CountAsync(c => c.VenueId == venue.Id && c.Active, ct);

            var bookedMinutes = confirmed.Sum(b => b.Minutes);
            var openMinutes = period is { } p ? (int)(p.Closes - p.Opens).TotalMinutes * courts : 0;
            var occupancy = openMinutes == 0 ? 0 : (int)Math.Round(100.0 * bookedMinutes / openMinutes);

            var next = await StaffBookingEndpoints.LoadAsync(db, venue, inWindow
                .Where(b => b.Status == BookingStatus.Confirmed && b.EndsAt > now)
                .OrderBy(b => b.StartsAt).Take(5), ct);

            return Results.Ok(new TodayDto(
                day, period is not null, TimeZoneInfo.ConvertTime(from, zone), TimeZoneInfo.ConvertTime(to, zone),
                confirmed.Count, bookedMinutes, Math.Min(occupancy, 100), confirmed.Sum(b => b.Total),
                venue.CurrencyCode, cancelled, next));
        });
    }
}
