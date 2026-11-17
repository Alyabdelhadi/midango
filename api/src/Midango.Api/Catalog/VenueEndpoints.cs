using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;
using Midango.Api.Venues;

namespace Midango.Api.Catalog;

public static class VenueEndpoints
{
    public sealed record HoursDto(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);

    public sealed record BandDto(DayOfWeek Day, TimeOnly From, TimeOnly To, decimal HourlyPrice);

    public sealed record CourtDto(
        Guid Id, Guid CategoryId, string NameEn, string NameAr, bool Indoor, decimal HourlyPrice,
        IReadOnlyList<Guid> FeatureIds, int SortOrder, bool Active, IReadOnlyList<BandDto> Bands)
    {
        public static CourtDto From(Court c, IEnumerable<PriceBand> bands) => new(
            c.Id, c.CategoryId, c.NameEn, c.NameAr, c.Indoor, c.HourlyPrice,
            c.Features.Select(f => f.Id).ToList(), c.SortOrder, c.Active,
            bands.OrderBy(b => b.Day).ThenBy(b => b.From).Select(b => new BandDto(b.Day, b.From, b.To, b.HourlyPrice)).ToList());
    }

    /// missingForPublish holds keys the client translates: "address", "location", "hours", "courts".
    public sealed record VenueDto(
        Guid Id, string NameEn, string NameAr, string? AddressEn, string? AddressAr,
        double? Latitude, double? Longitude, string TimeZoneId, string CurrencyCode, decimal VatPercent, int FreeCancellationHours, bool Published,
        IReadOnlyList<HoursDto> Hours, IReadOnlyList<CourtDto> Courts, IReadOnlyList<string> MissingForPublish);

    public sealed record VenueInput(
        string? NameEn, string? NameAr, string? AddressEn, string? AddressAr,
        double? Latitude, double? Longitude, string? TimeZoneId, int? FreeCancellationHours);

    public sealed record CourtInput(
        Guid CategoryId, string? NameEn, string? NameAr, bool Indoor, decimal HourlyPrice,
        Guid[]? FeatureIds, int SortOrder, bool Active);

    public static void MapVenueEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/venue", async (ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
            await LoadAsync(db, user.StaffVenueId()!.Value, ct) is { } venue
                ? Results.Ok(venue)
                : ApiErrors.Fail(404, "Venue not found."))
            .RequireAuthorization(Policies.VenueStaff);

        var manage = app.MapGroup("/venue").RequireAuthorization(Policies.VenueManager);

        manage.MapPut("", async (VenueInput input, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            if (ValidateVenue(input) is { } error) return ApiErrors.Fail(400, error);
            var venueId = user.StaffVenueId()!.Value;
            var venue = await db.Venues.FindAsync([venueId], ct);
            if (venue is null) return ApiErrors.Fail(404, "Venue not found.");

            venue.NameEn = input.NameEn!.Trim();
            venue.NameAr = input.NameAr!.Trim();
            venue.AddressEn = Blank(input.AddressEn);
            venue.AddressAr = Blank(input.AddressAr);
            venue.Latitude = input.Latitude;
            venue.Longitude = input.Longitude;
            venue.TimeZoneId = input.TimeZoneId ?? venue.TimeZoneId;
            venue.FreeCancellationHours = input.FreeCancellationHours ?? venue.FreeCancellationHours;

            if (await PublishGuardAsync(db, venue, null, null, ct) is { } refused) return refused;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await LoadAsync(db, venueId, ct));
        });

        manage.MapPut("/hours", async (HoursDto[] input, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            if (input.Length > 7 || input.Select(h => h.Day).Distinct().Count() != input.Length
                || input.Any(h => !Enum.IsDefined(h.Day)))
                return ApiErrors.Fail(400, "Give each day of the week at most once.");
            if (input.Any(h => h.Opens == h.Closes))
                return ApiErrors.Fail(400, "Opening and closing time can't be the same. Leave the day out to close it.");

            var venueId = user.StaffVenueId()!.Value;
            var venue = await db.Venues.FindAsync([venueId], ct);
            if (venue is null) return ApiErrors.Fail(404, "Venue not found.");
            if (await PublishGuardAsync(db, venue, input.Length, null, ct) is { } refused) return refused;

            var existing = await db.OpeningHours.Where(h => h.VenueId == venueId).ToListAsync(ct);
            db.OpeningHours.RemoveRange(existing.Where(e => input.All(i => i.Day != e.Day)));
            foreach (var day in input)
            {
                var row = existing.FirstOrDefault(e => e.Day == day.Day);
                if (row is null)
                {
                    row = new OpeningHours { VenueId = venueId, Day = day.Day };
                    db.OpeningHours.Add(row);
                }
                row.Opens = day.Opens;
                row.Closes = day.Closes;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(await LoadAsync(db, venueId, ct));
        });

        manage.MapPost("/courts", async (CourtInput input, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var court = new Court { VenueId = venueId, NameEn = "", NameAr = "" };
            if (await ApplyCourtAsync(db, court, input, ct) is { } error) return ApiErrors.Fail(400, error);
            db.Courts.Add(court);
            await db.SaveChangesAsync(ct);
            return Results.Json(await LoadAsync(db, venueId, ct), statusCode: 201);
        });

        manage.MapPut("/courts/{id:guid}", async (
            Guid id, CourtInput input, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var court = await db.Courts.Include(c => c.Features)
                .SingleOrDefaultAsync(c => c.Id == id && c.VenueId == venueId, ct);
            if (court is null) return ApiErrors.Fail(404, "Court not found.");

            var wasActive = court.Active;
            if (await ApplyCourtAsync(db, court, input, ct) is { } error) return ApiErrors.Fail(400, error);

            if (wasActive && !court.Active)
            {
                var venue = (await db.Venues.FindAsync([venueId], ct))!;
                var activeAfter = await db.Courts.CountAsync(c => c.VenueId == venueId && c.Active && c.Id != id, ct);
                if (await PublishGuardAsync(db, venue, null, activeAfter, ct) is { } refused) return refused;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(await LoadAsync(db, venueId, ct));
        });

        manage.MapPost("/publish", async (ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var current = await LoadAsync(db, venueId, ct);
            if (current is null) return ApiErrors.Fail(404, "Venue not found.");
            if (current.MissingForPublish.Count > 0)
                return Results.Problem(statusCode: 409, title: "Finish the venue before publishing.",
                    extensions: new Dictionary<string, object?> { ["missing"] = current.MissingForPublish });

            await db.Venues.Where(v => v.Id == venueId).ExecuteUpdateAsync(s => s.SetProperty(v => v.Published, true), ct);
            return Results.Ok(await LoadAsync(db, venueId, ct));
        });

        manage.MapPost("/unpublish", async (ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            await db.Venues.Where(v => v.Id == venueId).ExecuteUpdateAsync(s => s.SetProperty(v => v.Published, false), ct);
            return Results.Ok(await LoadAsync(db, venueId, ct));
        });
    }

    public static IReadOnlyList<string> MissingForPublish(Venue venue, int openDays, int activeCourts)
    {
        var missing = new List<string>();
        if (venue.AddressEn is null || venue.AddressAr is null) missing.Add("address");
        if (venue.Latitude is null || venue.Longitude is null) missing.Add("location");
        if (openDays == 0) missing.Add("hours");
        if (activeCourts == 0) missing.Add("courts");
        return missing;
    }

    /// A published venue must stay complete. Pass the counts the pending change would leave;
    /// null means "unchanged, read it from the database". Returns a 409 to send back, or null to go ahead.
    private static async Task<IResult?> PublishGuardAsync(
        MidangoDb db, Venue venue, int? openDays, int? activeCourts, CancellationToken ct)
    {
        if (!venue.Published) return null;
        openDays ??= await db.OpeningHours.CountAsync(h => h.VenueId == venue.Id, ct);
        activeCourts ??= await db.Courts.CountAsync(c => c.VenueId == venue.Id && c.Active, ct);
        var missing = MissingForPublish(venue, openDays.Value, activeCourts.Value);
        return missing.Count == 0
            ? null
            : Results.Problem(statusCode: 409, title: "A live venue must keep its address, map location, opening hours and at least one open court. Unpublish it first to change that.",
                extensions: new Dictionary<string, object?> { ["missing"] = missing });
    }

    internal static async Task<VenueDto?> LoadAsync(MidangoDb db, Guid venueId, CancellationToken ct)
    {
        var venue = await db.Venues.AsNoTracking().SingleOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null) return null;
        var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venueId).OrderBy(h => h.Day).ToListAsync(ct);
        var courts = await db.Courts.AsNoTracking().Include(c => c.Features)
            .Where(c => c.VenueId == venueId).OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);

        var courtIds = courts.Select(c => c.Id).ToList();
        var bands = await db.PriceBands.AsNoTracking().Where(b => courtIds.Contains(b.CourtId)).ToListAsync(ct);

        return new VenueDto(
            venue.Id, venue.NameEn, venue.NameAr, venue.AddressEn, venue.AddressAr,
            venue.Latitude, venue.Longitude, venue.TimeZoneId, venue.CurrencyCode, venue.VatPercent, venue.FreeCancellationHours, venue.Published,
            hours.Select(h => new HoursDto(h.Day, h.Opens, h.Closes)).ToList(),
            courts.Select(c => CourtDto.From(c, bands.Where(b => b.CourtId == c.Id))).ToList(),
            MissingForPublish(venue, hours.Count, courts.Count(c => c.Active)));
    }

    private static string? ValidateVenue(VenueInput input)
    {
        if (!Input.Bilingual(input.NameEn, input.NameAr, 120))
            return "Enter the venue name in English and Arabic, up to 120 characters each.";
        var hasEn = !string.IsNullOrWhiteSpace(input.AddressEn);
        var hasAr = !string.IsNullOrWhiteSpace(input.AddressAr);
        if (hasEn != hasAr || (hasEn && !Input.Bilingual(input.AddressEn, input.AddressAr, 200)))
            return "Enter the address in both English and Arabic, up to 200 characters each.";
        if ((input.Latitude is null) != (input.Longitude is null)
            || input.Latitude is < -90 or > 90 || input.Longitude is < -180 or > 180)
            return "Pick the venue's location on the map.";
        if (input.TimeZoneId is not null && !TimeZoneInfo.TryFindSystemTimeZoneById(input.TimeZoneId, out _))
            return "Choose a valid time zone.";
        if (input.FreeCancellationHours is < 0 or > 168)
            return "Free cancellation must be between 0 and 168 hours before the start.";
        return null;
    }

    /// Validates and copies the input onto the court. Returns an error sentence, or null when applied.
    private static async Task<string?> ApplyCourtAsync(MidangoDb db, Court court, CourtInput input, CancellationToken ct)
    {
        if (!Input.Bilingual(input.NameEn, input.NameAr, 60))
            return "Enter the court name in English and Arabic, up to 60 characters each.";
        if (!Pricing.ValidPrice(input.HourlyPrice))
            return "Enter the court's price per hour between 1 and 10,000, with at most 2 decimals.";

        var category = await db.SportCategories.Include(c => c.Features).SingleOrDefaultAsync(c => c.Id == input.CategoryId, ct);
        if (category is null || (!category.Active && category.Id != court.CategoryId))
            return "Choose one of the available sports.";

        var featureIds = input.FeatureIds ?? [];
        var features = category.Features.Where(f => featureIds.Contains(f.Id)).ToList();
        if (features.Count != featureIds.Distinct().Count())
            return "Choose features that belong to this court's sport.";

        court.CategoryId = category.Id;
        court.NameEn = input.NameEn!.Trim();
        court.NameAr = input.NameAr!.Trim();
        court.Indoor = input.Indoor;
        court.HourlyPrice = input.HourlyPrice;
        court.SortOrder = input.SortOrder;
        court.Active = input.Active;
        court.Features.Clear();
        court.Features.AddRange(features);
        return null;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
