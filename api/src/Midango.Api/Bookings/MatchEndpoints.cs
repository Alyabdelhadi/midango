using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Catalog;
using Midango.Api.Data;
using Midango.Api.Identity;
using Midango.Api.Notifications;

namespace Midango.Api.Bookings;

/// Open matches: an organiser opens their booking, others join and pay an equal share at the desk.
public static class MatchEndpoints
{
    public sealed record OpenMatchBody(int MaxPlayers, PlayLevel? Level);

    public sealed record LevelDto(Guid CategoryId, PlayLevel Level);

    public sealed record LevelBody(PlayLevel? Level);

    public sealed record MatchPlayerDto(Guid CustomerId, string FirstName);

    /// On BookingDto. Players are the organiser first, then active joiners in joining order.
    public sealed record BookingMatchDto(int MaxPlayers, PlayLevel? Level, decimal Share, IReadOnlyList<MatchPlayerDto> Players);

    public sealed record MatchNameDto(string FirstName);

    /// Times carry the venue's own offset. Players are first names only, organiser first.
    public sealed record MatchDto(
        Guid Id, Guid VenueId, Guid CategoryId, string VenueNameEn, string VenueNameAr, string? VenueLogoUrl,
        string CourtNameEn, string CourtNameAr, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Minutes,
        string Currency, decimal Share, int MaxPlayers, int SpotsLeft, PlayLevel? Level, IReadOnlyList<MatchNameDto> Players,
        string OrganiserFirstName, string? AddressEn, string? AddressAr, DateTimeOffset FreeCancellationUntil);

    public static void MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var customer = app.MapGroup("").RequireAuthorization(Policies.Customer);

        customer.MapGet("/me/levels", async (ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            return Results.Ok(await db.CustomerLevels.AsNoTracking().Where(l => l.CustomerId == customerId)
                .Select(l => new LevelDto(l.CategoryId, l.Level)).ToListAsync(ct));
        });

        customer.MapPut("/me/levels/{categoryId:guid}", async (
            Guid categoryId, LevelBody body, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            if (body.Level is { } wanted && !Enum.IsDefined(wanted)) return ApiErrors.Fail(400, "Choose a level.");
            var customerId = user.SubjectId();
            if (!await db.SportCategories.AnyAsync(c => c.Id == categoryId && c.Active, ct)) return ApiErrors.Fail(404, "Sport not found.");

            var row = await db.CustomerLevels.SingleOrDefaultAsync(l => l.CustomerId == customerId && l.CategoryId == categoryId, ct);
            if (body.Level is not { } level)
            {
                if (row is not null) db.CustomerLevels.Remove(row);
            }
            else if (row is null) db.CustomerLevels.Add(new CustomerLevel { CustomerId = customerId, CategoryId = categoryId, Level = level });
            else row.Level = level;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Public, like /venues. `date` is the venue-local business day; `level` also shows any-level matches.
        app.MapGet("/matches", async (
            Guid categoryId, DateOnly date, PlayLevel? level, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            if (!await db.SportCategories.AnyAsync(c => c.Id == categoryId && c.Active, ct)) return ApiErrors.Fail(400, "Choose a sport.");

            var now = clock.GetUtcNow();
            // A business day can run past midnight and venues sit east of UTC: take a wide UTC window, then the exact day per venue.
            var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
            var to = from.AddDays(3);
            var candidates = await db.Bookings.AsNoTracking()
                .Where(b => b.IsOpenMatch && b.MaxPlayers != null && b.Status == BookingStatus.Confirmed
                            && b.StartsAt > now && b.StartsAt >= from && b.StartsAt < to
                            && db.Courts.Any(c => c.Id == b.CourtId && c.CategoryId == categoryId)
                            && db.Venues.Any(v => v.Id == b.VenueId && v.Published)
                            && (level == null || b.MatchLevel == null || b.MatchLevel == level)
                            && 1 + db.MatchPlayers.Count(p => p.BookingId == b.Id && p.LeftAt == null) < b.MaxPlayers)
                .Select(b => new { b.Id, b.VenueId, b.StartsAt })
                .ToListAsync(ct);

            var venueIds = candidates.Select(c => c.VenueId).Distinct().ToList();
            var zones = await db.Venues.AsNoTracking().Where(v => venueIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => VenueClock.Zone(v), ct);
            var hours = (await db.OpeningHours.AsNoTracking().Where(h => venueIds.Contains(h.VenueId)).ToListAsync(ct))
                .GroupBy(h => h.VenueId).ToDictionary(g => g.Key, g => (IReadOnlyList<OpeningHours>)g.ToList());

            var ids = candidates.Where(c =>
            {
                var local = VenueClock.ToLocal(c.StartsAt, zones[c.VenueId]);
                return Slots.Period(date, hours.GetValueOrDefault(c.VenueId) ?? []) is { } day && local >= day.Opens && local < day.Closes;
            }).Select(c => c.Id).ToList();

            var matches = await LoadMatchesAsync(db, db.Bookings.Where(b => ids.Contains(b.Id)), ct);
            return Results.Ok(matches.OrderBy(m => m.StartsAt));
        });

        app.MapGet("/matches/{id:guid}", async (Guid id, MidangoDb db, CancellationToken ct) =>
        {
            var match = (await LoadMatchesAsync(db, db.Bookings.Where(b =>
                b.Id == id && b.IsOpenMatch && b.Status == BookingStatus.Confirmed
                && db.Venues.Any(v => v.Id == b.VenueId && v.Published)), ct)).SingleOrDefault();
            return match is null ? ApiErrors.Fail(404, "Match not found.") : Results.Ok(match);
        });

        customer.MapPost("/matches/{id:guid}/join", async (
            Guid id, ClaimsPrincipal user, MidangoDb db, MatchNotifications notify, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            var me = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == customerId, ct);
            if (me is null) return ApiErrors.Fail(404, "Account not found.");
            if (me.Name is null) return ApiErrors.Fail(409, "Add your name before joining. The venue uses it at the desk.");

            var now = clock.GetUtcNow();
            bool full;
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                // Locked until commit, so two joins for the last spot can't both see it free.
                var booking = await LockAsync(db, id, ct);
                if (booking is not { IsOpenMatch: true, MaxPlayers: { } max, Status: BookingStatus.Confirmed }
                    || !await db.Venues.AnyAsync(v => v.Id == booking.VenueId && v.Published, ct))
                    return ApiErrors.Fail(404, "Match not found.");
                if (booking.CustomerId == customerId) return ApiErrors.Fail(409, "This is your own match.");
                if (booking.StartsAt <= now) return ApiErrors.Fail(409, "This match has already started.");

                var players = await db.MatchPlayers.Where(p => p.BookingId == id).ToListAsync(ct);
                var row = players.FirstOrDefault(p => p.CustomerId == customerId);
                if (row is { LeftAt: null }) return ApiErrors.Fail(409, "You're already in this match.");
                if (row is { RemovedByOrganiser: true }) return ApiErrors.Fail(409, "The organiser removed you from this match.");
                var active = players.Count(p => p.LeftAt == null);
                if (1 + active >= max) return ApiErrors.Fail(409, "This match is full.");
                if (await BookingEndpoints.NoShowLimitAsync(db, customerId, now, ct) is { } limited) return limited;

                if (row is null) db.MatchPlayers.Add(new MatchPlayer { BookingId = id, CustomerId = customerId, JoinedAt = now });
                else
                {
                    row.JoinedAt = now;
                    row.LeftAt = null;
                    row.LateLeave = false;
                    row.ReminderSentAt = null;
                }
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                full = 2 + active == max;
            }

            await notify.JoinedAsync(id, FirstName(me.Name), full);
            return Results.Ok((await LoadMatchesAsync(db, db.Bookings.Where(b => b.Id == id), ct)).Single());
        });

        customer.MapPost("/matches/{id:guid}/leave", async (
            Guid id, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, ct);
            var row = await db.MatchPlayers.SingleOrDefaultAsync(p => p.BookingId == id && p.CustomerId == customerId && p.LeftAt == null, ct);
            if (booking is null || row is null) return ApiErrors.Fail(404, "You're not in this match.");
            if (booking.Status != BookingStatus.Confirmed) return ApiErrors.Fail(409, "This match was cancelled.");
            var now = clock.GetUtcNow();
            if (booking.StartsAt <= now) return ApiErrors.Fail(409, "This match has already started.");

            row.LeftAt = now;
            row.LateLeave = now > booking.FreeCancellationUntil;
            await db.SaveChangesAsync(ct);
            return Results.Ok((await LoadMatchesAsync(db, db.Bookings.Where(b => b.Id == id), ct)).Single());
        });

        customer.MapPost("/bookings/{id:guid}/open", async (
            Guid id, OpenMatchBody body, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                var booking = await LockAsync(db, id, ct);
                if (booking is null || booking.CustomerId != customerId) return ApiErrors.Fail(404, "Booking not found.");
                if (booking.Status != BookingStatus.Confirmed)
                    return ApiErrors.Fail(409, "Only a confirmed booking can be opened to other players.");
                if (booking.StartsAt <= clock.GetUtcNow()) return ApiErrors.Fail(409, "This booking has already started.");

                var playersPerMatch = await (from c in db.Courts
                                             join s in db.SportCategories on c.CategoryId equals s.Id
                                             where c.Id == booking.CourtId
                                             select s.PlayersPerMatch).SingleAsync(ct);
                var taken = 1 + await db.MatchPlayers.CountAsync(p => p.BookingId == id && p.LeftAt == null, ct);
                if (ValidateOpen(body, playersPerMatch, taken) is { } error) return error;

                booking.IsOpenMatch = true;
                booking.MaxPlayers = body.MaxPlayers;
                booking.MatchLevel = body.Level;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            return Results.Ok((await BookingEndpoints.LoadAsync(db, db.Bookings.Where(b => b.Id == id), customerId, ct)).Single());
        });

        customer.MapPost("/bookings/{id:guid}/close", async (Guid id, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                var booking = await LockAsync(db, id, ct);
                if (booking is null || booking.CustomerId != customerId) return ApiErrors.Fail(404, "Booking not found.");
                if (await db.MatchPlayers.AnyAsync(p => p.BookingId == id && p.LeftAt == null, ct))
                    return ApiErrors.Fail(409, "Remove the players who joined first.");

                booking.IsOpenMatch = false;
                booking.MaxPlayers = null;
                booking.MatchLevel = null;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            return Results.Ok((await BookingEndpoints.LoadAsync(db, db.Bookings.Where(b => b.Id == id), customerId, ct)).Single());
        });

        customer.MapDelete("/bookings/{id:guid}/players/{playerId:guid}", async (
            Guid id, Guid playerId, ClaimsPrincipal user, MidangoDb db, MatchNotifications notify, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id && b.CustomerId == customerId, ct);
            if (booking is null) return ApiErrors.Fail(404, "Booking not found.");
            var now = clock.GetUtcNow();
            if (booking.StartsAt <= now) return ApiErrors.Fail(409, "This match has already started.");
            var row = await db.MatchPlayers.SingleOrDefaultAsync(p => p.BookingId == id && p.CustomerId == playerId && p.LeftAt == null, ct);
            if (row is null) return ApiErrors.Fail(404, "That player isn't in this match.");

            row.LeftAt = now;
            row.RemovedByOrganiser = true;
            await db.SaveChangesAsync(ct);
            await notify.RemovedAsync(id, playerId);
            return Results.Ok((await BookingEndpoints.LoadAsync(db, db.Bookings.Where(b => b.Id == id), customerId, ct)).Single());
        });
    }

    /// Checks a request to open (or re-open) a match. `taken` = organiser + active joiners.
    internal static IResult? ValidateOpen(OpenMatchBody body, int playersPerMatch, int taken)
    {
        if (body.Level is { } level && !Enum.IsDefined(level)) return ApiErrors.Fail(400, "Choose a level.");
        if (body.MaxPlayers < 2 || body.MaxPlayers > playersPerMatch)
            return ApiErrors.Fail(400, $"Choose between 2 and {playersPerMatch} players for this sport.");
        if (body.MaxPlayers < taken)
            return ApiErrors.Fail(409, $"{taken} players are already in this match. Allow at least {taken}, or remove someone first.");
        return null;
    }

    /// The equal share everyone sees. The organiser pays the total minus the joiners' shares.
    internal static decimal Share(decimal total, int maxPlayers) => Math.Round(total / maxPlayers, 2, MidpointRounding.AwayFromZero);

    /// What the organiser pays at the desk: the total minus each active joiner's share.
    internal static decimal OrganiserShare(decimal total, int maxPlayers, int activeJoiners) =>
        total - Share(total, maxPlayers) * activeJoiners;

    /// First word of a name; phone numbers never leave the server.
    internal static string FirstName(string? name) =>
        name?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";

    private static async Task<Booking?> LockAsync(MidangoDb db, Guid id, CancellationToken ct) =>
        (await db.Bookings.FromSql($"SELECT * FROM bookings WHERE id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();

    internal sealed record PersonRow(Guid BookingId, Guid CustomerId, string? Name, DateTimeOffset JoinedAt, DateTimeOffset? LeftAt, bool RemovedByOrganiser);

    /// Joiner rows (any state) and organiser names for a set of bookings.
    internal sealed class People(ILookup<Guid, PersonRow> rows, Dictionary<Guid, string?> names)
    {
        public IEnumerable<PersonRow> Rows(Guid bookingId) => rows[bookingId];

        public List<PersonRow> Active(Guid bookingId) =>
            rows[bookingId].Where(p => p.LeftAt == null).OrderBy(p => p.JoinedAt).ToList();

        public string OrganiserFirstName(Booking b) => FirstName(b.CustomerId is { } id ? names.GetValueOrDefault(id) : null);
    }

    internal static async Task<People> LoadPeopleAsync(MidangoDb db, IReadOnlyList<Booking> bookings, CancellationToken ct)
    {
        var ids = bookings.Select(b => b.Id).ToList();
        var rows = await (from p in db.MatchPlayers
                          join c in db.Customers on p.CustomerId equals c.Id
                          where ids.Contains(p.BookingId)
                          select new PersonRow(p.BookingId, p.CustomerId, c.Name, p.JoinedAt, p.LeftAt, p.RemovedByOrganiser))
            .AsNoTracking().ToListAsync(ct);
        var organiserIds = bookings.Where(b => b.IsOpenMatch && b.CustomerId != null).Select(b => b.CustomerId!.Value).Distinct().ToList();
        var names = await db.Customers.AsNoTracking().Where(c => organiserIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return new People(rows.ToLookup(r => r.BookingId), names);
    }

    /// The match part of a BookingDto; null when the booking isn't open.
    internal static BookingMatchDto? BookingMatch(Booking b, People people)
    {
        if (!b.IsOpenMatch || b.MaxPlayers is not { } max) return null;
        List<MatchPlayerDto> players = [new(b.CustomerId!.Value, people.OrganiserFirstName(b))];
        players.AddRange(people.Active(b.Id).Select(p => new MatchPlayerDto(p.CustomerId, FirstName(p.Name))));
        return new BookingMatchDto(max, b.MatchLevel, Share(b.Total, max), players);
    }

    internal static async Task<List<MatchDto>> LoadMatchesAsync(MidangoDb db, IQueryable<Booking> query, CancellationToken ct)
    {
        var rows = await (
            from b in query
            where b.MaxPlayers != null
            join c in db.Courts on b.CourtId equals c.Id
            join v in db.Venues on b.VenueId equals v.Id
            select new { Booking = b, c.CategoryId, CourtEn = c.NameEn, CourtAr = c.NameAr, Venue = v })
            .AsNoTracking().ToListAsync(ct);
        var people = await LoadPeopleAsync(db, rows.Select(r => r.Booking).ToList(), ct);

        return rows.Select(r =>
        {
            var b = r.Booking;
            var v = r.Venue;
            var zone = VenueClock.Zone(v);
            DateTimeOffset Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, zone);
            var max = b.MaxPlayers!.Value;
            var organiser = people.OrganiserFirstName(b);
            var active = people.Active(b.Id);
            List<MatchNameDto> players = [new(organiser), .. active.Select(p => new MatchNameDto(FirstName(p.Name)))];
            return new MatchDto(
                b.Id, b.VenueId, r.CategoryId, v.NameEn, v.NameAr, v.LogoPath, r.CourtEn, r.CourtAr,
                Local(b.StartsAt), Local(b.EndsAt), b.Minutes, b.Currency, Share(b.Total, max), max,
                Math.Max(0, max - players.Count), b.MatchLevel, players,
                organiser, v.AddressEn, v.AddressAr, Local(b.FreeCancellationUntil));
        }).ToList();
    }
}
