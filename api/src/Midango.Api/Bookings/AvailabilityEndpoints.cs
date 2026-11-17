using Microsoft.EntityFrameworkCore;
using Midango.Api.Catalog;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Bookings;

public static class AvailabilityEndpoints
{
    public sealed record SlotCourtDto(Guid CourtId, decimal Total);

    /// Start carries the venue's offset (e.g. +03:00) so clients can send it straight back when booking.
    public sealed record SlotDto(DateTimeOffset Start, string LocalTime, IReadOnlyList<SlotCourtDto> Courts);

    public sealed record AvailabilityDto(
        Guid VenueId, DateOnly Date, Guid CategoryId, int Minutes, string Currency, bool Open, IReadOnlyList<SlotDto> Slots);

    public static void MapAvailabilityEndpoints(this IEndpointRouteBuilder app)
    {
        // Public: the app's slot chips. Full times stay in the list with no courts, so they can be struck through.
        app.MapGet("/venues/{venueId:guid}/availability", async (
            Guid venueId, DateOnly date, Guid categoryId, int minutes, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleOrDefaultAsync(v => v.Id == venueId && v.Published, ct);
            if (venue is null) return ApiErrors.Fail(404, "Venue not found.");

            var category = await db.SportCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == categoryId && c.Active, ct);
            if (category is null || !category.DurationsMinutes.Contains(minutes))
                return ApiErrors.Fail(400, "Choose one of this sport's booking lengths.");

            var zone = VenueClock.Zone(venue);
            var now = clock.GetUtcNow();
            var today = DateOnly.FromDateTime(VenueClock.ToLocal(now, zone));
            if (date < today || date > today.AddDays(Slots.MaxDaysAhead))
                return ApiErrors.Fail(400, $"Choose a date within the next {Slots.MaxDaysAhead} days.");

            var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venueId).ToListAsync(ct);
            var courts = await db.Courts.AsNoTracking()
                .Where(c => c.VenueId == venueId && c.CategoryId == categoryId && c.Active)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);
            var courtIds = courts.Select(c => c.Id).ToList();
            var bands = (await db.PriceBands.AsNoTracking().Where(b => courtIds.Contains(b.CourtId)).ToListAsync(ct))
                .ToLookup(b => b.CourtId, b => new PriceBandRule(b.Day, b.From, b.To, b.HourlyPrice));

            var starts = Slots.Starts(date, hours, minutes).ToList();
            if (starts.Count == 0)
                return Results.Ok(new AvailabilityDto(venue.Id, date, categoryId, minutes, venue.CurrencyCode, false, []));

            var windowFrom = VenueClock.ToInstant(starts[0], zone).ToUniversalTime();
            var windowTo = VenueClock.ToInstant(starts[^1].AddMinutes(minutes), zone).ToUniversalTime();
            var booked = await db.Bookings.AsNoTracking()
                .Where(b => courtIds.Contains(b.CourtId) && b.Status == BookingStatus.Confirmed
                            && b.StartsAt < windowTo && b.EndsAt > windowFrom)
                .Select(b => new { b.CourtId, b.StartsAt, b.EndsAt })
                .ToListAsync(ct);

            // ponytail: courts × times × bookings in memory; fine for a venue-day (≈ 20 courts × 40 times).
            var slots = new List<SlotDto>();
            foreach (var local in starts)
            {
                var start = VenueClock.ToInstant(local, zone);
                var end = start.AddMinutes(minutes);
                if (start <= now) continue;

                var free = courts
                    .Where(c => !booked.Any(b => b.CourtId == c.Id && b.StartsAt < end && b.EndsAt > start))
                    .Select(c => new SlotCourtDto(c.Id, Pricing.Calculate(
                        c.HourlyPrice, bands[c.Id].ToList(), local.DayOfWeek, TimeOnly.FromDateTime(local), minutes, venue.VatPercent).Total))
                    .ToList();
                slots.Add(new SlotDto(start, local.ToString("HH:mm"), free));
            }

            return Results.Ok(new AvailabilityDto(venue.Id, date, categoryId, minutes, venue.CurrencyCode, true, slots));
        });
    }
}
