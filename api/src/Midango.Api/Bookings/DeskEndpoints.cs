using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;
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
            ChangeAsync(db, user, id, ct, booking =>
            {
                if (booking.CheckedInAt is not null) return null; // already done: same answer again
                if (booking.Status != BookingStatus.Confirmed) return (409, "Only a confirmed booking can be checked in.");
                var now = clock.GetUtcNow();
                if (now < booking.StartsAt.AddMinutes(-CheckInOpensMinutesBefore))
                    return (409, $"Check-in opens {CheckInOpensMinutesBefore} minutes before the start.");
                if (now > booking.EndsAt.AddHours(CheckInClosesHoursAfter))
                    return (409, "This booking finished too long ago to check in.");
                booking.CheckedInAt = now;
                booking.CheckedInByStaffId = user.SubjectId();
                booking.CashCollected = booking.Total;
                return null;
            }));

        desk.MapPost("/{id:guid}/undo-check-in", (Guid id, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(db, user, id, ct, booking =>
            {
                if (booking.CheckedInAt is not { } at) return null;
                if (clock.GetUtcNow() - at > UndoWindow) return (409, "A check-in can only be undone within 15 minutes.");
                booking.CheckedInAt = null;
                booking.CheckedInByStaffId = null;
                booking.CashCollected = null;
                return null;
            }));

        desk.MapPost("/{id:guid}/no-show", (Guid id, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(db, user, id, ct, booking =>
            {
                if (booking.Status == BookingStatus.NoShow) return null;
                if (booking.Status != BookingStatus.Confirmed) return (409, "Only a confirmed booking can be marked as a no-show.");
                if (booking.CheckedInAt is not null) return (409, "This customer has checked in. Undo the check-in first.");
                var now = clock.GetUtcNow();
                if (now < booking.StartsAt) return (409, "Wait until the booking has started.");
                booking.Status = BookingStatus.NoShow;
                booking.NoShowAt = now;
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
    }

    private static Task<Venue> VenueOfAsync(MidangoDb db, ClaimsPrincipal user, CancellationToken ct) =>
        db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);

    /// Loads one of the caller's venue bookings, applies `change` (which returns an error or null), saves and
    /// returns the booking. Re-confirming a no-show goes through the exclusion constraint like any booking.
    private static async Task<IResult> ChangeAsync(
        MidangoDb db, ClaimsPrincipal user, Guid id, CancellationToken ct, Func<Booking, (int Status, string Title)?> change)
    {
        var venue = await VenueOfAsync(db, user, ct);
        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == id && b.VenueId == venue.Id, ct);
        if (booking is null) return ApiErrors.Fail(404, "Booking not found.");
        if (booking.Kind == BookingKind.Block) return ApiErrors.Fail(400, "A block has nobody to check in.");

        if (change(booking) is { } error) return ApiErrors.Fail(error.Status, error.Title);
        if (await BookingStore.SaveAsync(db, [], ct) is not null)
            return ApiErrors.Fail(409, "The court has been booked again since. The no-show stays.");

        return Results.Ok((await StaffBookingEndpoints.LoadAsync(db, venue, db.Bookings.Where(b => b.Id == id), ct)).Single());
    }
}
