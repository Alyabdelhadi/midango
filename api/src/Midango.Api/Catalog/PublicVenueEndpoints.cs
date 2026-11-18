using Microsoft.EntityFrameworkCore;
using Midango.Api.Bookings;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Catalog;

/// What the app shows before anyone signs in: venue cards with open times, and a venue page.
public static class PublicVenueEndpoints
{
    public const int CardTimes = 8;

    public sealed record VenueCardDto(
        Guid Id, string NameEn, string NameAr, string? AddressEn, string? AddressAr, double? Latitude, double? Longitude,
        string Currency, int Courts, decimal? FromTotal, bool Open, IReadOnlyList<AvailabilityEndpoints.SlotDto> Times,
        string? LogoUrl, string? PhotoUrl);

    public sealed record PublicFeatureDto(string NameEn, string NameAr, string? IconUrl);

    public sealed record PublicCourtDto(
        Guid Id, Guid CategoryId, string NameEn, string NameAr, bool Indoor, IReadOnlyList<PublicFeatureDto> Features);

    public sealed record PublicCategoryDto(Guid Id, string NameEn, string NameAr, string IconKey, int[] DurationsMinutes, string? IconUrl);

    public sealed record PublicVenueDto(
        Guid Id, string NameEn, string NameAr, string? AddressEn, string? AddressAr, double? Latitude, double? Longitude,
        string Currency, decimal VatPercent, int FreeCancellationHours,
        IReadOnlyList<VenueEndpoints.HoursDto> Hours, IReadOnlyList<PublicCategoryDto> Categories, IReadOnlyList<PublicCourtDto> Courts,
        string? LogoUrl, IReadOnlyList<string> Photos);

    public static void MapPublicVenueEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/venues", async (
            Guid categoryId, DateOnly date, int minutes, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var category = await db.SportCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == categoryId && c.Active, ct);
            if (category is null || !category.DurationsMinutes.Contains(minutes))
                return ApiErrors.Fail(400, "Choose one of this sport's booking lengths.");

            var venues = await db.Venues.AsNoTracking()
                .Where(v => v.Published && db.Courts.Any(c => c.VenueId == v.Id && c.CategoryId == categoryId && c.Active))
                .ToListAsync(ct);
            var courtCounts = await db.Courts.AsNoTracking()
                .Where(c => c.CategoryId == categoryId && c.Active)
                .GroupBy(c => c.VenueId).Select(g => new { VenueId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.VenueId, g => g.Count, ct);
            var venueIds = venues.Select(v => v.Id).ToList();
            var firstPhotos = await db.VenuePhotos.AsNoTracking()
                .Where(p => venueIds.Contains(p.VenueId))
                .GroupBy(p => p.VenueId)
                .Select(g => new { VenueId = g.Key, Path = g.OrderBy(p => p.SortOrder).Select(p => p.Path).First() })
                .ToDictionaryAsync(g => g.VenueId, g => g.Path, ct);

            var now = clock.GetUtcNow();
            var cards = new List<VenueCardDto>();
            // ponytail: one availability calculation per venue, in sequence. Fine for tens of venues;
            // precompute per day or page the list when a city has hundreds.
            foreach (var venue in venues)
            {
                var today = DateOnly.FromDateTime(VenueClock.ToLocal(now, VenueClock.Zone(venue)));
                var inRange = date >= today && date <= today.AddDays(Slots.MaxDaysAhead);
                var day = inRange ? await AvailabilityEndpoints.ComputeAsync(db, venue, categoryId, date, minutes, now, ct) : null;
                var free = day?.Slots.Where(s => s.Courts.Count > 0).ToList() ?? [];
                cards.Add(new VenueCardDto(
                    venue.Id, venue.NameEn, venue.NameAr, venue.AddressEn, venue.AddressAr, venue.Latitude, venue.Longitude,
                    venue.CurrencyCode, courtCounts.GetValueOrDefault(venue.Id),
                    free.Count == 0 ? null : free.SelectMany(s => s.Courts).Min(c => c.Total),
                    day?.Open ?? false, free.Take(CardTimes).ToList(),
                    venue.LogoPath, firstPhotos.GetValueOrDefault(venue.Id)));
            }

            return Results.Ok(cards.OrderByDescending(c => c.Times.Count > 0).ThenBy(c => c.NameEn));
        });

        app.MapGet("/venues/{id:guid}", async (Guid id, MidangoDb db, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id && v.Published, ct);
            if (venue is null) return ApiErrors.Fail(404, "Venue not found.");

            var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == id).OrderBy(h => h.Day).ToListAsync(ct);
            var courts = await db.Courts.AsNoTracking().Include(c => c.Features)
                .Where(c => c.VenueId == id && c.Active).OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);
            var categoryIds = courts.Select(c => c.CategoryId).Distinct().ToList();
            var categories = await db.SportCategories.AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id) && c.Active).OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);
            var shown = categories.Select(c => c.Id).ToHashSet();
            var photos = await db.VenuePhotos.AsNoTracking().Where(p => p.VenueId == id).OrderBy(p => p.SortOrder)
                .Select(p => p.Path).ToListAsync(ct);

            return Results.Ok(new PublicVenueDto(
                venue.Id, venue.NameEn, venue.NameAr, venue.AddressEn, venue.AddressAr, venue.Latitude, venue.Longitude,
                venue.CurrencyCode, venue.VatPercent, venue.FreeCancellationHours,
                hours.Select(h => new VenueEndpoints.HoursDto(h.Day, h.Opens, h.Closes)).ToList(),
                categories.Select(c => new PublicCategoryDto(c.Id, c.NameEn, c.NameAr, c.IconKey, c.DurationsMinutes, c.IconPath)).ToList(),
                courts.Where(c => shown.Contains(c.CategoryId))
                    .Select(c => new PublicCourtDto(c.Id, c.CategoryId, c.NameEn, c.NameAr, c.Indoor,
                        c.Features.Select(f => new PublicFeatureDto(f.NameEn, f.NameAr, f.IconPath)).ToList()))
                    .ToList(),
                venue.LogoPath, photos));
        });
    }
}
