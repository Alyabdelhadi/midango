using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Bookings;
using Midango.Api.Data;
using Midango.Api.Loyalty;
using Midango.Api.Tournaments;

namespace Midango.Api.Identity;

/// The operator's view of app customers across every venue: search, activity, block, lift the no-show limit.
/// Midango support sees full phone numbers (venue staff never do).
public static class AdminCustomerEndpoints
{
    public const int PageSize = 50;

    public sealed record CustomerListItem(
        Guid Id, string? Name, string Phone, DateTimeOffset CreatedAt, int Bookings, int NoShowsInWindow, bool Limited, bool Blocked);
    public sealed record CustomerList(IReadOnlyList<CustomerListItem> Items, int Total);

    public sealed record CustomerStats(
        int Bookings, int CheckedIn, int Cancelled, int LateCancellations, int NoShows, int NoShowsInWindow,
        int MatchesJoined, int TournamentTeams);
    public sealed record CustomerPoints(int Balance, string Tier);

    /// Times carry the venue's offset. For a joined match, check-in / no-show / late are the player's own.
    public sealed record CustomerBooking(
        Guid Id, string Reference, string VenueNameEn, string VenueNameAr, string CourtNameEn, string CourtNameAr,
        DateTimeOffset StartsAt, int Minutes, decimal Total, string Currency, BookingStatus Status, string Role,
        DateTimeOffset? CheckedInAt, DateTimeOffset? NoShowAt, bool LateCancellation, bool CancelledByVenue);
    public sealed record CustomerTournament(
        Guid TournamentId, string NameEn, string NameAr, string TeamName, TeamStatus Status, DateTimeOffset StartsAt);

    public sealed record CustomerDetail(
        Guid Id, string? Name, string Phone, DateTimeOffset CreatedAt,
        DateTimeOffset? BlockedAt, string? BlockedReason, string? BlockedBy, DateTimeOffset? NoShowsClearedAt,
        CustomerStats Stats, bool Limited, CustomerPoints Points, int Devices,
        IReadOnlyList<CustomerBooking> Bookings, IReadOnlyList<CustomerTournament> Tournaments);

    public sealed record BlockBody(string? Reason);

    public static void MapAdminCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var customers = app.MapGroup("/admin/customers").RequireAuthorization(Policies.PlatformOperator);

        customers.MapGet("", async (string? q, int? page, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var query = db.Customers.AsNoTracking();
            var term = q?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(term))
            {
                var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
                // A local "05…" is stored as "+9665…".
                var international = digits.StartsWith('0') ? "966" + digits[1..] : digits;
                query = digits.Length >= 3
                    ? query.Where(c => (c.Name != null && c.Name.ToLower().Contains(term))
                                       || c.PhoneE164.Contains(digits) || c.PhoneE164.Contains(international))
                    : query.Where(c => c.Name != null && c.Name.ToLower().Contains(term));
            }

            var total = await query.CountAsync(ct);
            var rows = await query
                .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
                .Skip((Math.Max(page ?? 1, 1) - 1) * PageSize).Take(PageSize)
                .Select(c => new { c.Id, c.Name, c.PhoneE164, c.CreatedAt, Blocked = c.BlockedAt != null })
                .ToListAsync(ct);

            var ids = rows.Select(r => r.Id).ToList();
            var bookings = await db.Bookings
                .Where(b => b.CustomerId != null && ids.Contains(b.CustomerId.Value))
                .GroupBy(b => b.CustomerId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
            var rules = await db.PlatformSettings.AsNoTracking().SingleAsync(ct);
            var standing = await BookingEndpoints.NoShowStandingAsync(db, rules, ids, clock.GetUtcNow(), ct);

            return Results.Ok(new CustomerList(rows.Select(r => new CustomerListItem(
                r.Id, r.Name, r.PhoneE164, r.CreatedAt, bookings.GetValueOrDefault(r.Id),
                standing[r.Id].Missed, standing[r.Id].Limited, r.Blocked)).ToList(), total));
        });

        customers.MapGet("/{id:guid}", async (Guid id, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            await DetailAsync(db, id, clock.GetUtcNow(), ct) is { } detail
                ? Results.Ok(detail)
                : ApiErrors.Fail(404, "Customer not found."));

        customers.MapPost("/{id:guid}/block", async (
            Guid id, BlockBody body, ClaimsPrincipal user, MidangoDb db, TokenService tokens, TimeProvider clock, CancellationToken ct) =>
        {
            var reason = body.Reason?.Trim();
            if (reason is not { Length: >= 3 and <= 300 })
                return ApiErrors.Fail(400, "Enter a reason of 3 to 300 characters.");
            var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
            if (customer is null) return ApiErrors.Fail(404, "Customer not found.");

            if (customer.BlockedAt is null) // blocking twice keeps the first block
            {
                customer.BlockedAt = clock.GetUtcNow();
                customer.BlockedReason = reason;
                customer.BlockedByStaffId = user.SubjectId();
                await db.SaveChangesAsync(ct);
            }
            // Every time, so a session that slipped in can't outlive the block.
            await tokens.RevokeAllAsync(SubjectKind.Customer, id, ct);
            return Results.Ok(await DetailAsync(db, id, clock.GetUtcNow(), ct));
        });

        customers.MapPost("/{id:guid}/unblock", async (Guid id, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
            if (customer is null) return ApiErrors.Fail(404, "Customer not found.");
            customer.BlockedAt = null;
            customer.BlockedReason = null;
            customer.BlockedByStaffId = null;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await DetailAsync(db, id, clock.GetUtcNow(), ct));
        });

        customers.MapPost("/{id:guid}/lift-no-show-limit", async (Guid id, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
            if (customer is null) return ApiErrors.Fail(404, "Customer not found.");
            customer.NoShowsClearedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Results.Ok(await DetailAsync(db, id, clock.GetUtcNow(), ct));
        });
    }

    private static async Task<CustomerDetail?> DetailAsync(MidangoDb db, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;

        var blockedBy = c.BlockedByStaffId is { } staffId
            ? await db.Staff.Where(s => s.Id == staffId).Select(s => s.DisplayName).SingleOrDefaultAsync(ct)
            : null;

        var own = await db.Bookings.Where(b => b.CustomerId == id).GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                CheckedIn = g.Count(b => b.CheckedInAt != null),
                Cancelled = g.Count(b => b.Status == BookingStatus.Cancelled),
                Late = g.Count(b => b.LateCancellation),
                NoShows = g.Count(b => b.Status == BookingStatus.NoShow),
            }).SingleOrDefaultAsync(ct);
        var joined = await db.MatchPlayers.Where(p => p.CustomerId == id).GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                CheckedIn = g.Count(p => p.CheckedInAt != null),
                Late = g.Count(p => p.LateLeave),
                NoShows = g.Count(p => p.NoShowAt != null),
            }).SingleOrDefaultAsync(ct);
        var teams = await db.TournamentTeams.CountAsync(t => t.CaptainCustomerId == id, ct);
        var devices = await db.CustomerDevices.CountAsync(d => d.CustomerId == id, ct);

        var rules = await db.PlatformSettings.AsNoTracking().SingleAsync(ct);
        var standing = (await BookingEndpoints.NoShowStandingAsync(db, rules, [id], now, ct))[id];
        var balance = await Points.BalanceAsync(db, id, ct);
        var tier = await Points.TierAsync(db, rules, id, now, ct);

        // Own bookings and matches joined (one MatchPlayers row per booking and customer at most).
        var bookingRows = await (
            from b in db.Bookings
            join p in db.MatchPlayers.Where(p => p.CustomerId == id) on b.Id equals p.BookingId into ps
            from p in ps.DefaultIfEmpty()
            where b.CustomerId == id || p != null
            join court in db.Courts on b.CourtId equals court.Id
            join v in db.Venues on b.VenueId equals v.Id
            orderby b.StartsAt descending, b.Id descending
            select new
            {
                b.Id, b.Reference, VenueEn = v.NameEn, VenueAr = v.NameAr, CourtEn = court.NameEn, CourtAr = court.NameAr,
                v.TimeZoneId, b.StartsAt, b.Minutes, b.Total, b.Currency, b.Status, Organiser = b.CustomerId == id,
                CheckedInAt = b.CustomerId == id ? b.CheckedInAt : p!.CheckedInAt,
                NoShowAt = b.CustomerId == id ? b.NoShowAt : p!.NoShowAt,
                Late = b.CustomerId == id ? b.LateCancellation : p!.LateLeave,
                b.CancelledByVenue,
            })
            .Take(50).AsNoTracking().ToListAsync(ct);

        var tournamentRows = await (
            from team in db.TournamentTeams
            where team.CaptainCustomerId == id
            join t in db.Tournaments on team.TournamentId equals t.Id
            join v in db.Venues on t.VenueId equals v.Id
            orderby t.StartsAt descending
            select new { t.Id, t.NameEn, t.NameAr, TeamName = team.Name, team.Status, t.StartsAt, v.TimeZoneId })
            .AsNoTracking().ToListAsync(ct);

        static DateTimeOffset Local(DateTimeOffset instant, string zoneId) =>
            TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(zoneId));

        return new CustomerDetail(
            c.Id, c.Name, c.PhoneE164, c.CreatedAt, c.BlockedAt, c.BlockedReason, blockedBy, c.NoShowsClearedAt,
            new CustomerStats(
                own?.Count ?? 0,
                (own?.CheckedIn ?? 0) + (joined?.CheckedIn ?? 0),
                own?.Cancelled ?? 0,
                (own?.Late ?? 0) + (joined?.Late ?? 0),
                (own?.NoShows ?? 0) + (joined?.NoShows ?? 0),
                standing.Missed,
                joined?.Count ?? 0,
                teams),
            standing.Limited,
            new CustomerPoints(balance, tier.Name),
            devices,
            bookingRows.Select(r => new CustomerBooking(
                r.Id, r.Reference, r.VenueEn, r.VenueAr, r.CourtEn, r.CourtAr,
                Local(r.StartsAt, r.TimeZoneId), r.Minutes, r.Total, r.Currency, r.Status,
                r.Organiser ? BookingEndpoints.Organiser : BookingEndpoints.Player,
                r.CheckedInAt is { } ci ? Local(ci, r.TimeZoneId) : null,
                r.NoShowAt is { } ns ? Local(ns, r.TimeZoneId) : null,
                r.Late, r.CancelledByVenue)).ToList(),
            tournamentRows.Select(r => new CustomerTournament(
                r.Id, r.NameEn, r.NameAr, r.TeamName, r.Status, Local(r.StartsAt, r.TimeZoneId))).ToList());
    }
}
