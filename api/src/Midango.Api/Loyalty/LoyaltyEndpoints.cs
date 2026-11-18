using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Bookings;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Loyalty;

public static class LoyaltyEndpoints
{
    public sealed record RedeemRulesDto(int PointsPerStep, decimal SarPerStep, int MaxPercent);

    /// `at` is UTC. Venue names are null for an entry without a booking.
    public sealed record PointsHistoryDto(
        DateTimeOffset At, int Points, PointsKind Kind, Guid? BookingId, string? VenueNameEn, string? VenueNameAr);

    public sealed record PointsDto(
        int Balance, string Tier, int GamesInWindow, NextTier? NextTier, decimal Multiplier,
        RedeemRulesDto Redeem, IReadOnlyList<PointsHistoryDto> History);

    public sealed record RedeemBody(int Points);

    public static void MapLoyaltyEndpoints(this IEndpointRouteBuilder app)
    {
        var customer = app.MapGroup("").RequireAuthorization(Policies.Customer);

        customer.MapGet("/me/points", async (ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            var s = await db.PlatformSettings.AsNoTracking().SingleAsync(ct);
            var tier = await Points.TierAsync(db, s, customerId, clock.GetUtcNow(), ct);
            var balance = await Points.BalanceAsync(db, customerId, ct);
            var history = await (
                from e in db.PointsEntries
                where e.CustomerId == customerId
                join b in db.Bookings on e.BookingId equals (Guid?)b.Id into bookings
                from b in bookings.DefaultIfEmpty()
                join v in db.Venues on b.VenueId equals v.Id into venues
                from v in venues.DefaultIfEmpty()
                orderby e.At descending, e.Id descending
                select new PointsHistoryDto(e.At, e.Points, e.Kind, e.BookingId, v == null ? null : v.NameEn, v == null ? null : v.NameAr))
                .Take(50).AsNoTracking().ToListAsync(ct);

            return Results.Ok(new PointsDto(
                balance, tier.Name, tier.GamesInWindow, tier.Next, tier.Multiplier,
                new RedeemRulesDto(s.PointsPerRedeemStep, s.SarPerRedeemStep, s.MaxRedeemPercent), history));
        });

        // The organiser or an active joiner puts points on their own amount of an upcoming booking.
        customer.MapPost("/bookings/{id:guid}/points", async (
            Guid id, RedeemBody body, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            if (await Customers.BlockedAsync(db, customerId, ct) is { } blocked) return blocked;
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                // The customer row is the balance lock: redeems by the same customer queue here, so the
                // balance can't be spent twice. Then the booking, so a join can't change the organiser's amount meanwhile.
                await db.Database.ExecuteSqlAsync($"SELECT 1 FROM customers WHERE id = {customerId} FOR UPDATE", ct);
                var booking = await MatchEndpoints.LockAsync(db, id, ct);
                if (booking is null || booking.Kind != BookingKind.Booking) return ApiErrors.Fail(404, "Booking not found.");
                MatchPlayer? player = null;
                if (booking.CustomerId != customerId)
                {
                    player = await db.MatchPlayers.SingleOrDefaultAsync(p => p.BookingId == id && p.CustomerId == customerId && p.LeftAt == null, ct);
                    if (player is null || booking.MaxPlayers is null) return ApiErrors.Fail(404, "Booking not found.");
                }

                if (await Points.RedeemAsync(db, customerId, booking, player, body.Points, clock.GetUtcNow(), ct) is { } error)
                    return ApiErrors.Fail(error.Status, error.Title);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            return Results.Ok((await BookingEndpoints.LoadAsync(db, db.Bookings.Where(b => b.Id == id), customerId, ct)).Single());
        });
    }
}
