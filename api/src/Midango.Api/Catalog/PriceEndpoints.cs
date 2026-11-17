using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Catalog;

public static class PriceEndpoints
{
    public sealed record BandInput(DayOfWeek Day, TimeOnly From, TimeOnly To, decimal HourlyPrice);

    public sealed record PricesInput(Guid[]? CourtIds, BandInput[]? Bands);

    public sealed record QuoteDto(
        Guid CourtId, DayOfWeek Day, TimeOnly Start, int Minutes, decimal Total, decimal Net, decimal Vat,
        decimal VatPercent, string Currency, IReadOnlyList<PriceSegment> Segments);

    public static void MapPriceEndpoints(this IEndpointRouteBuilder app)
    {
        // Replaces the peak/off-peak periods of every listed court with the same board.
        app.MapPut("/venue/prices", async (PricesInput input, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var courtIds = input.CourtIds?.Distinct().ToArray() ?? [];
            if (courtIds.Length == 0) return ApiErrors.Fail(400, "Choose at least one court.");
            var owned = await db.Courts.CountAsync(c => courtIds.Contains(c.Id) && c.VenueId == venueId, ct);
            if (owned != courtIds.Length) return ApiErrors.Fail(404, "Court not found.");

            var bands = input.Bands ?? [];
            if (ValidateBands(bands) is { } error) return ApiErrors.Fail(400, error);

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.PriceBands.Where(b => courtIds.Contains(b.CourtId)).ExecuteDeleteAsync(ct);
            db.PriceBands.AddRange(courtIds.SelectMany(courtId => bands.Select(b => new PriceBand
            {
                CourtId = courtId,
                Day = b.Day,
                From = b.From,
                To = b.To,
                HourlyPrice = b.HourlyPrice,
            })));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Ok(await VenueEndpoints.LoadAsync(db, venueId, ct));
        }).RequireAuthorization(Policies.VenueManager);

        // What a booking costs. Desk staff use this at the counter.
        app.MapGet("/venue/quote", async (
            Guid courtId, DayOfWeek day, TimeOnly time, int minutes, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var court = await db.Courts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == courtId && c.VenueId == venueId, ct);
            if (court is null) return ApiErrors.Fail(404, "Court not found.");
            if (!Enum.IsDefined(day) || !Pricing.OnQuarterHour(time))
                return ApiErrors.Fail(400, "Choose a day and a start time on the quarter hour.");

            var durations = await db.SportCategories.Where(c => c.Id == court.CategoryId).Select(c => c.DurationsMinutes).SingleAsync(ct);
            if (!durations.Contains(minutes)) return ApiErrors.Fail(400, "Choose one of this sport's booking lengths.");

            var venue = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == venueId, ct);
            var bands = await db.PriceBands.AsNoTracking().Where(b => b.CourtId == courtId)
                .Select(b => new PriceBandRule(b.Day, b.From, b.To, b.HourlyPrice)).ToListAsync(ct);

            var quote = Pricing.Calculate(court.HourlyPrice, bands, day, time, minutes, venue.VatPercent);
            return Results.Ok(new QuoteDto(
                court.Id, day, time, minutes, quote.Total, quote.Net, quote.Vat, venue.VatPercent, venue.CurrencyCode, quote.Segments));
        }).RequireAuthorization(Policies.VenueStaff);
    }

    private static string? ValidateBands(BandInput[] bands)
    {
        if (bands.Length > 50) return "Use at most 50 price periods.";
        foreach (var band in bands)
        {
            if (!Enum.IsDefined(band.Day)) return "Choose a day for every price period.";
            if (band.From == band.To) return "A price period can't start and end at the same time.";
            if (!Pricing.OnQuarterHour(band.From) || !Pricing.OnQuarterHour(band.To))
                return "Price periods start and end on the quarter hour (:00, :15, :30, :45).";
            if (!Pricing.ValidPrice(band.HourlyPrice))
                return "Enter a price per hour between 1 and 10,000, with at most 2 decimals.";
        }

        var rules = bands.Select(b => new PriceBandRule(b.Day, b.From, b.To, b.HourlyPrice)).ToList();
        for (var i = 0; i < rules.Count; i++)
        for (var j = i + 1; j < rules.Count; j++)
        {
            if (Pricing.Overlaps(rules[i], rules[j]))
                return $"Two price periods overlap ({rules[i].Day} {rules[i].From:HH\\:mm} and {rules[j].Day} {rules[j].From:HH\\:mm}).";
        }
        return null;
    }
}
