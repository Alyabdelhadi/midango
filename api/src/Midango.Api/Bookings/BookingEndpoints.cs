using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Catalog;
using Midango.Api.Data;
using Midango.Api.Identity;
using Npgsql;

namespace Midango.Api.Bookings;

public static class BookingEndpoints
{
    public const string CashAtVenue = "CashAtVenue";

    public sealed record CreateBookingBody(Guid CourtId, DateTimeOffset Start, int Minutes);

    /// Times carry the venue's own offset (e.g. +03:00).
    public sealed record BookingDto(
        Guid Id, string Reference, Guid VenueId, string VenueNameEn, string VenueNameAr,
        Guid CourtId, string CourtNameEn, string CourtNameAr,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Minutes,
        decimal Total, decimal Net, decimal Vat, string Currency, string PaymentMethod,
        BookingStatus Status, DateTimeOffset FreeCancellationUntil, bool LateCancellation, DateTimeOffset? CancelledAt,
        bool CancelledByVenue);

    public sealed record MyBookingsDto(IReadOnlyList<BookingDto> Upcoming, IReadOnlyList<BookingDto> History);

    public static void MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var customer = app.MapGroup("").RequireAuthorization(Policies.Customer);

        customer.MapPost("/bookings", async (
            CreateBookingBody body, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            var me = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == customerId, ct);
            if (me is null) return ApiErrors.Fail(404, "Account not found.");
            if (me.Name is null) return ApiErrors.Fail(409, "Add your name before booking. The venue uses it at the desk.");

            var court = await db.Courts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == body.CourtId && c.Active, ct);
            if (court is null) return ApiErrors.Fail(404, "Court not found.");
            var venue = await db.Venues.AsNoTracking().SingleOrDefaultAsync(v => v.Id == court.VenueId && v.Published, ct);
            if (venue is null) return ApiErrors.Fail(404, "Court not found.");
            var durations = await db.SportCategories.Where(c => c.Id == court.CategoryId && c.Active)
                .Select(c => c.DurationsMinutes).SingleOrDefaultAsync(ct);
            if (durations is null || !durations.Contains(body.Minutes))
                return ApiErrors.Fail(400, "Choose one of this sport's booking lengths.");

            var now = clock.GetUtcNow();
            var start = body.Start.ToUniversalTime();
            if (start <= now) return ApiErrors.Fail(400, "That time has already passed.");
            if (start > now.AddDays(Slots.MaxDaysAhead + 1))
                return ApiErrors.Fail(400, $"Book within the next {Slots.MaxDaysAhead} days.");

            // Customers who keep not turning up can only hold a few bookings at a time (M9 rules, across venues).
            var rules = await db.PlatformSettings.AsNoTracking().SingleAsync(ct);
            var since = now.AddDays(-rules.NoShowWindowDays);
            var missed = await db.Bookings.CountAsync(
                b => b.CustomerId == customerId && b.Status == BookingStatus.NoShow && b.NoShowAt > since, ct);
            if (missed >= rules.NoShowLimit)
            {
                var upcoming = await db.Bookings.CountAsync(
                    b => b.CustomerId == customerId && b.Status == BookingStatus.Confirmed && b.EndsAt > now, ct);
                if (upcoming >= rules.LimitedUpcomingBookings)
                    return ApiErrors.Fail(409,
                        $"You missed {missed} bookings in the last {rules.NoShowWindowDays} days, so for now you can hold " +
                        $"{rules.LimitedUpcomingBookings} upcoming booking{(rules.LimitedUpcomingBookings == 1 ? "" : "s")} at a time.");
            }

            var zone = VenueClock.Zone(venue);
            var local = VenueClock.ToLocal(start, zone);
            var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venue.Id).ToListAsync(ct);
            if (!Slots.Bookable(local, body.Minutes, hours))
                return ApiErrors.Fail(400, "That isn't one of the venue's start times, or it runs past closing.");

            var bands = await db.PriceBands.AsNoTracking().Where(b => b.CourtId == court.Id)
                .Select(b => new PriceBandRule(b.Day, b.From, b.To, b.HourlyPrice)).ToListAsync(ct);
            var price = Pricing.Calculate(court.HourlyPrice, bands, local.DayOfWeek, TimeOnly.FromDateTime(local), body.Minutes, venue.VatPercent);

            var booking = new Booking
            {
                Reference = BookingReference.New(),
                Kind = BookingKind.Booking,
                Source = BookingSource.App,
                VenueId = venue.Id,
                CourtId = court.Id,
                CustomerId = customerId,
                StartsAt = start,
                EndsAt = start.AddMinutes(body.Minutes),
                Minutes = body.Minutes,
                Total = price.Total,
                Net = price.Net,
                Vat = price.Vat,
                Currency = venue.CurrencyCode,
                CreatedAt = now,
                FreeCancellationUntil = start.AddHours(-venue.FreeCancellationHours),
            };
            db.Bookings.Add(booking);

            if (await BookingStore.SaveAsync(db, [booking], ct) is { } failed) return failed;

            var dto = (await LoadAsync(db, db.Bookings.Where(b => b.Id == booking.Id), ct)).Single();
            return Results.Json(dto, statusCode: 201);
        });

        customer.MapGet("/me/bookings", async (ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            var now = clock.GetUtcNow();
            var upcoming = await LoadAsync(db, db.Bookings
                .Where(b => b.CustomerId == customerId && b.Status == BookingStatus.Confirmed && b.EndsAt > now)
                .OrderBy(b => b.StartsAt), ct);
            var history = await LoadAsync(db, db.Bookings
                .Where(b => b.CustomerId == customerId && (b.Status != BookingStatus.Confirmed || b.EndsAt <= now))
                .OrderByDescending(b => b.StartsAt).Take(50), ct);
            return Results.Ok(new MyBookingsDto(upcoming, history));
        });

        customer.MapPost("/bookings/{id:guid}/cancel", async (
            Guid id, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == id && b.CustomerId == customerId, ct);
            if (booking is null) return ApiErrors.Fail(404, "Booking not found.");

            if (booking.Status == BookingStatus.Confirmed)
            {
                if (booking.CheckedInAt is not null) return ApiErrors.Fail(409, "You've already checked in at the venue.");
                var now = clock.GetUtcNow();
                if (booking.StartsAt <= now) return ApiErrors.Fail(409, "This booking has already started.");
                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = now;
                booking.LateCancellation = now > booking.FreeCancellationUntil;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok((await LoadAsync(db, db.Bookings.Where(b => b.Id == id), ct)).Single());
        });
    }

    private static async Task<List<BookingDto>> LoadAsync(MidangoDb db, IQueryable<Booking> query, CancellationToken ct)
    {
        var rows = await (
            from b in query
            join c in db.Courts on b.CourtId equals c.Id
            join v in db.Venues on b.VenueId equals v.Id
            select new { Booking = b, CourtEn = c.NameEn, CourtAr = c.NameAr, VenueEn = v.NameEn, VenueAr = v.NameAr, v.TimeZoneId })
            .AsNoTracking().ToListAsync(ct);

        return rows.Select(r =>
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(r.TimeZoneId);
            DateTimeOffset Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, zone);
            var b = r.Booking;
            return new BookingDto(
                b.Id, b.Reference, b.VenueId, r.VenueEn, r.VenueAr, b.CourtId, r.CourtEn, r.CourtAr,
                Local(b.StartsAt), Local(b.EndsAt), b.Minutes, b.Total, b.Net, b.Vat, b.Currency, CashAtVenue,
                b.Status, Local(b.FreeCancellationUntil), b.LateCancellation, b.CancelledAt is { } c ? Local(c) : null,
                b.CancelledByVenue);
        }).ToList();
    }
}

public static class BookingStore
{
    /// Saves pending changes. Returns a 409 when a booking overlaps a confirmed one (exclusion constraint),
    /// redraws references on the rare collision, and returns null on success.
    public static async Task<IResult?> SaveAsync(MidangoDb db, IReadOnlyList<Booking> added, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return null;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
            {
                return ApiErrors.Fail(409, "Someone just booked this time. Pick another one.");
            }
            catch (DbUpdateException ex) when (attempt < 3 && ex.InnerException is PostgresException
                   { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_bookings_reference" })
            {
                foreach (var b in added) b.Reference = BookingReference.New();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                   { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_customers_phone_e164" })
            {
                // Two desks created an account for the same new number at once; the retry finds it.
                return ApiErrors.Fail(409, "This customer was just added from another screen. Try again.");
            }
        }
    }
}
