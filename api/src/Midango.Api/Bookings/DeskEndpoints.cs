using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;
using Midango.Api.Loyalty;
using Midango.Api.Venues;

namespace Midango.Api.Bookings;

/// The counter: find a booking by reference, take cash and check in, mark a no-show, undo either.
public static class DeskEndpoints
{
    public const int CheckInOpensMinutesBefore = 60;
    public const int CheckInClosesHoursAfter = 6;
    public static readonly TimeSpan UndoWindow = TimeSpan.FromMinutes(15);

    public static void MapDeskEndpoints(this IEndpointRouteBuilder app)
    {
        var desk = app.MapGroup("/venue/bookings").RequireAuthorization(Policies.VenueStaff);

        // The app's QR code holds the bare reference; a scanner types it into the Desk's box.
        desk.MapGet("/lookup", async (string? reference, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var code = reference?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) return ApiErrors.Fail(400, "Type or scan a booking reference.");
            var venue = await VenueOfAsync(db, user, ct);
            var found = await StaffBookingEndpoints.LoadAsync(db, venue,
                db.Bookings.Where(b => b.VenueId == venue.Id && b.Kind == BookingKind.Booking && b.Reference == code), ct);
            return found.Count == 0 ? ApiErrors.Fail(404, "No booking with that reference here.") : Results.Ok(found[0]);
        });

        desk.MapPost("/{id:guid}/check-in", (Guid id, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(db, user, id, ct, async booking =>
            {
                if (booking.CheckedInAt is not null) return null; // already done: same answer again
                var now = clock.GetUtcNow();
                if (CheckInError(booking, now) is { } error) return error;
                booking.CheckedInAt = now;
                booking.CheckedInByStaffId = user.SubjectId();
                // The organiser of an open match pays only their part; each joiner pays a share at their own check-in.
                // Cash is what is actually paid: any points discount is owed to the venue by Midango.
                booking.CashCollected = Points.AmountToPay(await Points.OrganiserAmountAsync(db, booking, ct), booking.PointsDiscount);
                await Points.EarnAsync(db, booking.CustomerId, booking.Id, booking.CashCollected.Value, now, ct);
                return null;
            }));

        desk.MapPost("/{id:guid}/undo-check-in", (Guid id, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(db, user, id, ct, async booking =>
            {
                if (booking.CheckedInAt is not { } at) return null;
                var now = clock.GetUtcNow();
                if (now - at > UndoWindow) return (409, "A check-in can only be undone within 15 minutes.");
                booking.CheckedInAt = null;
                booking.CheckedInByStaffId = null;
                booking.CashCollected = null;
                await Points.ReverseEarnAsync(db, booking.CustomerId, booking.Id, now, ct);
                return null;
            }));

        desk.MapPost("/{id:guid}/no-show", (Guid id, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(db, user, id, ct, async booking =>
            {
                if (booking.Status == BookingStatus.NoShow) return null;
                if (booking.Status != BookingStatus.Confirmed) return (409, "Only a confirmed booking can be marked as a no-show.");
                if (booking.CheckedInAt is not null) return (409, "This customer has checked in. Undo the check-in first.");
                var now = clock.GetUtcNow();
                if (now < booking.StartsAt) return (409, "Wait until the booking has started.");
                booking.Status = BookingStatus.NoShow;
                booking.NoShowAt = now;
                // Points go back to the players; undoing the no-show doesn't put them back on the booking.
                await Points.ReleaseAllAsync(db, booking, now, ct);
                return null;
            }));

        desk.MapPost("/{id:guid}/undo-no-show", (Guid id, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(db, user, id, ct, booking =>
            {
                if (booking.Status != BookingStatus.NoShow) return null;
                if (clock.GetUtcNow() - booking.NoShowAt > UndoWindow) return (409, "A no-show can only be undone within 15 minutes.");
                booking.Status = BookingStatus.Confirmed;
                booking.NoShowAt = null;
                return null;
            }));

        // Open-match joiners, one by one. Staff can't remove a player: that's the organiser's choice.
        desk.MapPost("/{id:guid}/players/{customerId:guid}/check-in", (
            Guid id, Guid customerId, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangePlayerAsync(db, user, id, customerId, ct, async (booking, player) =>
            {
                if (player.CheckedInAt is not null) return (409, "This player has already checked in.");
                if (player.NoShowAt is not null) return (409, "This player is marked as a no-show. Undo it first.");
                var now = clock.GetUtcNow();
                if (CheckInError(booking, now) is { } error) return error;
                player.CheckedInAt = now;
                player.CashCollected = Points.AmountToPay(MatchEndpoints.Share(booking.Total, booking.MaxPlayers!.Value), player.PointsDiscount);
                await Points.EarnAsync(db, player.CustomerId, booking.Id, player.CashCollected.Value, now, ct);
                return null;
            }));

        desk.MapPost("/{id:guid}/players/{customerId:guid}/undo-check-in", (
            Guid id, Guid customerId, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangePlayerAsync(db, user, id, customerId, ct, async (booking, player) =>
            {
                if (player.CheckedInAt is not { } at) return null;
                var now = clock.GetUtcNow();
                if (now - at > UndoWindow) return (409, "A check-in can only be undone within 15 minutes.");
                player.CheckedInAt = null;
                player.CashCollected = null;
                await Points.ReverseEarnAsync(db, player.CustomerId, booking.Id, now, ct);
                return null;
            }));

        desk.MapPost("/{id:guid}/players/{customerId:guid}/no-show", (
            Guid id, Guid customerId, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangePlayerAsync(db, user, id, customerId, ct, (booking, player) =>
            {
                if (player.NoShowAt is not null) return null;
                if (booking.Status != BookingStatus.Confirmed) return (409, "Only a player in a confirmed booking can be marked as a no-show.");
                if (player.CheckedInAt is not null) return (409, "This player has checked in. Undo the check-in first.");
                var now = clock.GetUtcNow();
                if (now < booking.StartsAt) return (409, "Wait until the booking has started.");
                // The booking keeps its status: the court was used. This counts toward the player's no-show limit.
                player.NoShowAt = now;
                Points.Release(db, player, now);
                return null;
            }));

        desk.MapPost("/{id:guid}/players/{customerId:guid}/undo-no-show", (
            Guid id, Guid customerId, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangePlayerAsync(db, user, id, customerId, ct, (_, player) =>
            {
                if (player.NoShowAt is not { } at) return null;
                if (clock.GetUtcNow() - at > UndoWindow) return (409, "A no-show can only be undone within 15 minutes.");
                player.NoShowAt = null;
                return null;
            }));
    }

    /// The check-in rules shared by a booking and each of its match players.
    private static (int Status, string Title)? CheckInError(Booking booking, DateTimeOffset now)
    {
        if (booking.Status != BookingStatus.Confirmed) return (409, "Only a confirmed booking can be checked in.");
        if (now < booking.StartsAt.AddMinutes(-CheckInOpensMinutesBefore))
            return (409, $"Check-in opens {CheckInOpensMinutesBefore} minutes before the start.");
        if (now > booking.EndsAt.AddHours(CheckInClosesHoursAfter))
            return (409, "This booking finished too long ago to check in.");
        return null;
    }

    private static Task<IResult> ChangePlayerAsync(
        MidangoDb db, ClaimsPrincipal user, Guid id, Guid customerId, CancellationToken ct,
        Func<Booking, MatchPlayer, (int Status, string Title)?> change) =>
        ChangePlayerAsync(db, user, id, customerId, ct, (b, p) => Task.FromResult(change(b, p)));

    /// Like ChangeAsync, for one active joiner of one of the caller's venue bookings.
    private static async Task<IResult> ChangePlayerAsync(
        MidangoDb db, ClaimsPrincipal user, Guid id, Guid customerId, CancellationToken ct,
        Func<Booking, MatchPlayer, Task<(int Status, string Title)?>> change)
    {
        var venue = await VenueOfAsync(db, user, ct);
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id && b.VenueId == venue.Id, ct);
        if (booking is null) return ApiErrors.Fail(404, "Booking not found.");
        var player = await db.MatchPlayers.SingleOrDefaultAsync(p => p.BookingId == id && p.CustomerId == customerId && p.LeftAt == null, ct);
        if (player is null || booking.MaxPlayers is null) return ApiErrors.Fail(404, "That player isn't in this match.");

        if (await change(booking, player) is { } error) return ApiErrors.Fail(error.Status, error.Title);
        await db.SaveChangesAsync(ct);
        return Results.Ok((await StaffBookingEndpoints.LoadAsync(db, venue, db.Bookings.Where(b => b.Id == id), ct)).Single());
    }

    private static Task<Venue> VenueOfAsync(MidangoDb db, ClaimsPrincipal user, CancellationToken ct) =>
        db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);

    private static Task<IResult> ChangeAsync(
        MidangoDb db, ClaimsPrincipal user, Guid id, CancellationToken ct, Func<Booking, (int Status, string Title)?> change) =>
        ChangeAsync(db, user, id, ct, b => Task.FromResult(change(b)));

    /// Loads one of the caller's venue bookings, applies `change` (which returns an error or null), saves and
    /// returns the booking. Re-confirming a no-show goes through the exclusion constraint like any booking.
    private static async Task<IResult> ChangeAsync(
        MidangoDb db, ClaimsPrincipal user, Guid id, CancellationToken ct, Func<Booking, Task<(int Status, string Title)?>> change)
    {
        var venue = await VenueOfAsync(db, user, ct);
        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == id && b.VenueId == venue.Id, ct);
        if (booking is null) return ApiErrors.Fail(404, "Booking not found.");
        if (booking.Kind == BookingKind.Block) return ApiErrors.Fail(400, "A block has nobody to check in.");

        if (await change(booking) is { } error) return ApiErrors.Fail(error.Status, error.Title);
        if (await BookingStore.SaveAsync(db, [], ct) is not null)
            return ApiErrors.Fail(409, "The court has been booked again since. The no-show stays.");

        return Results.Ok((await StaffBookingEndpoints.LoadAsync(db, venue, db.Bookings.Where(b => b.Id == id), ct)).Single());
    }
}
