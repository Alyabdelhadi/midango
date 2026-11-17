using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Catalog;
using Midango.Api.Data;
using Midango.Api.Identity;
using Midango.Api.Venues;

namespace Midango.Api.Bookings;

public static class StaffBookingEndpoints
{
    public const int StaffMaxDaysAhead = 90;
    public const int MaxListDays = 62;

    /// Times carry the venue's offset. PhoneMasked is the only form of the number staff ever receive.
    public sealed record StaffBookingDto(
        Guid Id, string Reference, BookingKind Kind, BookingSource Source,
        Guid CourtId, string CourtNameEn, string CourtNameAr,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Minutes, decimal Total, string Currency, BookingStatus Status,
        Guid? CustomerId, string? CustomerName, string? PhoneMasked, string? Note,
        bool LateCancellation, bool CancelledByVenue, DateTimeOffset CreatedAt);

    public sealed record ScheduleCourtDto(Guid Id, Guid CategoryId, string NameEn, string NameAr, int[] DurationsMinutes);

    public sealed record ScheduleDto(
        DateOnly Date, bool Open, DateTimeOffset From, DateTimeOffset To, string Currency,
        IReadOnlyList<ScheduleCourtDto> Courts, IReadOnlyList<StaffBookingDto> Bookings);

    public sealed record StaffBookingBody(Guid CourtId, DateTimeOffset Start, int Minutes, string? Name, string? Phone, string? Note);

    public sealed record BlockBody(Guid[]? CourtIds, DateTimeOffset Start, int Minutes, string? Note);

    public sealed record MoveBody(Guid CourtId, DateTimeOffset Start);

    public static void MapStaffBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var venueStaff = app.MapGroup("/venue").RequireAuthorization(Policies.VenueStaff);

        venueStaff.MapGet("/schedule", async (DateOnly date, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);
            var zone = VenueClock.Zone(venue);
            var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venue.Id).ToListAsync(ct);
            var (fromLocal, toLocal) = Slots.DayWindow(date, hours);
            var from = VenueClock.ToInstant(fromLocal, zone);
            var to = VenueClock.ToInstant(toLocal, zone);
            var fromUtc = from.ToUniversalTime();
            var toUtc = to.ToUniversalTime();

            var courts = await (
                from c in db.Courts
                join s in db.SportCategories on c.CategoryId equals s.Id
                where c.VenueId == venue.Id && c.Active
                orderby c.SortOrder, c.NameEn
                select new ScheduleCourtDto(c.Id, c.CategoryId, c.NameEn, c.NameAr, s.DurationsMinutes))
                .AsNoTracking().ToListAsync(ct);

            var bookings = await LoadAsync(db, venue, db.Bookings.Where(b =>
                b.VenueId == venue.Id && b.Status == BookingStatus.Confirmed && b.StartsAt < toUtc && b.EndsAt > fromUtc), ct);

            return Results.Ok(new ScheduleDto(
                date, Slots.Period(date, hours) is not null, from, to, venue.CurrencyCode, courts, bookings));
        });

        venueStaff.MapPost("/bookings", async (
            StaffBookingBody body, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var name = body.Name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 60)
                return ApiErrors.Fail(400, "Enter the customer's name, up to 60 characters.");
            if (NoteError(body.Note) is { } noteError) return ApiErrors.Fail(400, noteError);
            PhoneNumber phone = default;
            var hasPhone = !string.IsNullOrWhiteSpace(body.Phone);
            if (hasPhone && !PhoneNumber.TryParse(body.Phone, out phone))
                return ApiErrors.Fail(400, "Enter a valid mobile number, or leave it empty for a walk-in.");

            var venue = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);
            var court = await db.Courts.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == body.CourtId && c.VenueId == venue.Id && c.Active, ct);
            if (court is null) return ApiErrors.Fail(404, "Court not found.");

            var now = clock.GetUtcNow();
            var start = body.Start.ToUniversalTime();
            if (await TimeErrorAsync(db, venue, start, body.Minutes, now, ct) is { } timeError)
                return ApiErrors.Fail(400, timeError);
            var local = VenueClock.ToLocal(start, VenueClock.Zone(venue));

            Guid? customerId = null;
            if (hasPhone)
            {
                var customer = await db.Customers.SingleOrDefaultAsync(c => c.PhoneE164 == phone.E164, ct);
                if (customer is null)
                {
                    customer = new Customer { PhoneE164 = phone.E164, Name = name };
                    db.Customers.Add(customer);
                }
                else customer.Name ??= name; // never overwrite the name the customer chose
                customerId = customer.Id;
            }

            var bands = await db.PriceBands.AsNoTracking().Where(b => b.CourtId == court.Id)
                .Select(b => new PriceBandRule(b.Day, b.From, b.To, b.HourlyPrice)).ToListAsync(ct);
            var price = Pricing.Calculate(court.HourlyPrice, bands, local.DayOfWeek, TimeOnly.FromDateTime(local), body.Minutes, venue.VatPercent);

            var booking = new Booking
            {
                Reference = BookingReference.New(),
                Kind = BookingKind.Booking,
                Source = BookingSource.Staff,
                CreatedByStaffId = user.SubjectId(),
                VenueId = venue.Id,
                CourtId = court.Id,
                CustomerId = customerId,
                GuestName = name,
                Note = Blank(body.Note),
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

            return Results.Json((await LoadAsync(db, venue, db.Bookings.Where(b => b.Id == booking.Id), ct)).Single(), statusCode: 201);
        });

        venueStaff.MapPost("/blocks", async (
            BlockBody body, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var courtIds = (body.CourtIds ?? []).Distinct().ToList();
            if (courtIds.Count == 0) return ApiErrors.Fail(400, "Choose at least one court to block.");
            if (NoteError(body.Note) is { } noteError) return ApiErrors.Fail(400, noteError);

            var venue = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);
            var found = await db.Courts.CountAsync(c => courtIds.Contains(c.Id) && c.VenueId == venue.Id && c.Active, ct);
            if (found != courtIds.Count) return ApiErrors.Fail(404, "Court not found.");

            var now = clock.GetUtcNow();
            var start = body.Start.ToUniversalTime();
            if (await TimeErrorAsync(db, venue, start, body.Minutes, now, ct) is { } timeError)
                return ApiErrors.Fail(400, timeError);

            var blocks = courtIds.Select(courtId => new Booking
            {
                Reference = BookingReference.New(),
                Kind = BookingKind.Block,
                Source = BookingSource.Staff,
                CreatedByStaffId = user.SubjectId(),
                VenueId = venue.Id,
                CourtId = courtId,
                Note = Blank(body.Note),
                StartsAt = start,
                EndsAt = start.AddMinutes(body.Minutes),
                Minutes = body.Minutes,
                Currency = venue.CurrencyCode,
                CreatedAt = now,
                FreeCancellationUntil = start,
            }).ToList();
            db.Bookings.AddRange(blocks);
            // One SaveChanges = one transaction: if any court is taken, no block is created.
            if (await BookingStore.SaveAsync(db, blocks, ct) is not null)
                return ApiErrors.Fail(409, "One of those courts already has a booking in that time. Nothing was blocked.");

            var ids = blocks.Select(b => b.Id).ToList();
            return Results.Json(await LoadAsync(db, venue, db.Bookings.Where(b => ids.Contains(b.Id)), ct), statusCode: 201);
        });

        venueStaff.MapPut("/bookings/{id:guid}/move", async (
            Guid id, MoveBody body, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);
            var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == id && b.VenueId == venue.Id, ct);
            if (booking is null) return ApiErrors.Fail(404, "Booking not found.");
            var now = clock.GetUtcNow();
            if (booking.Status != BookingStatus.Confirmed || booking.EndsAt <= now)
                return ApiErrors.Fail(409, "Only a booking that hasn't finished or been cancelled can be moved.");

            var target = await db.Courts.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == body.CourtId && c.VenueId == venue.Id && c.Active, ct);
            if (target is null) return ApiErrors.Fail(404, "Court not found.");
            if (booking.Kind == BookingKind.Booking && target.Id != booking.CourtId)
            {
                var currentCategory = await db.Courts.Where(c => c.Id == booking.CourtId).Select(c => c.CategoryId).SingleAsync(ct);
                if (currentCategory != target.CategoryId) return ApiErrors.Fail(400, "Move it to a court of the same sport.");
            }

            var start = body.Start.ToUniversalTime();
            if (await TimeErrorAsync(db, venue, start, booking.Minutes, now, ct) is { } timeError)
                return ApiErrors.Fail(400, timeError);

            booking.CourtId = target.Id;
            booking.StartsAt = start;
            booking.EndsAt = start.AddMinutes(booking.Minutes);
            booking.FreeCancellationUntil = booking.Kind == BookingKind.Block ? start : start.AddHours(-venue.FreeCancellationHours);
            if (await BookingStore.SaveAsync(db, [], ct) is { } failed) return failed;

            return Results.Ok((await LoadAsync(db, venue, db.Bookings.Where(b => b.Id == id), ct)).Single());
        });

        venueStaff.MapPost("/bookings/{id:guid}/cancel", async (
            Guid id, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);
            var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == id && b.VenueId == venue.Id, ct);
            if (booking is null) return ApiErrors.Fail(404, "Booking not found.");

            if (booking.Status == BookingStatus.Confirmed)
            {
                var now = clock.GetUtcNow();
                if (booking.EndsAt <= now) return ApiErrors.Fail(409, "This booking has already finished.");
                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = now;
                booking.CancelledByVenue = true;
                booking.LateCancellation = false;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok((await LoadAsync(db, venue, db.Bookings.Where(b => b.Id == id), ct)).Single());
        });

        venueStaff.MapGet("/bookings", async (
            DateOnly? from, DateOnly? to, BookingStatus? status, string? q,
            ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);
            var zone = VenueClock.Zone(venue);
            var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venue.Id).ToListAsync(ct);
            var today = Slots.BusinessDay(VenueClock.ToLocal(clock.GetUtcNow(), zone), hours);
            var first = from ?? today;
            var last = to ?? first;
            if (last < first || last.DayNumber - first.DayNumber >= MaxListDays)
                return ApiErrors.Fail(400, $"Choose a date range of up to {MaxListDays} days.");

            var windowFrom = VenueClock.ToInstant(Slots.DayWindow(first, hours).From, zone).ToUniversalTime();
            var windowTo = VenueClock.ToInstant(Slots.DayWindow(last, hours).To, zone).ToUniversalTime();
            var query = db.Bookings.Where(b => b.VenueId == venue.Id && b.Kind == BookingKind.Booking
                                               && b.StartsAt >= windowFrom && b.StartsAt < windowTo);
            if (status is { } s) query = query.Where(b => b.Status == s);

            var term = q?.Trim();
            if (!string.IsNullOrEmpty(term))
            {
                var pattern = $"%{Like(term)}%";
                var reference = term.ToUpperInvariant();
                var (phoneTail, phoneExact) = PhoneTerm(term);
                // Account names only match on app bookings, the same rule as what staff are shown (LoadAsync).
                query = query.Where(b =>
                    b.Reference == reference
                    || (b.GuestName != null && EF.Functions.ILike(b.GuestName, pattern))
                    || db.Customers.Any(c => c.Id == b.CustomerId
                        && ((b.Source == BookingSource.App && c.Name != null && EF.Functions.ILike(c.Name, pattern))
                            || (phoneTail != null && c.PhoneE164.EndsWith(phoneTail))
                            || c.PhoneE164 == phoneExact)));
            }

            return Results.Ok(await LoadAsync(db, venue, query.OrderBy(b => b.StartsAt).Take(500), ct));
        });
    }

    /// Loads bookings with court and customer names, sorted by start.
    internal static async Task<List<StaffBookingDto>> LoadAsync(MidangoDb db, Venue venue, IQueryable<Booking> query, CancellationToken ct)
    {
        var rows = await (
            from b in query
            join c in db.Courts on b.CourtId equals c.Id
            join cu in db.Customers on b.CustomerId equals (Guid?)cu.Id into customers
            from cu in customers.DefaultIfEmpty()
            select new { Booking = b, CourtEn = c.NameEn, CourtAr = c.NameAr, CustomerName = cu == null ? null : cu.Name, Phone = cu == null ? null : cu.PhoneE164 })
            .AsNoTracking().ToListAsync(ct);

        var zone = VenueClock.Zone(venue);
        DateTimeOffset Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, zone);
        return rows.OrderBy(r => r.Booking.StartsAt).Select(r =>
        {
            var b = r.Booking;
            return new StaffBookingDto(
                b.Id, b.Reference, b.Kind, b.Source, b.CourtId, r.CourtEn, r.CourtAr,
                Local(b.StartsAt), Local(b.EndsAt), b.Minutes, b.Total, b.Currency, b.Status,
                b.CustomerId, b.GuestName ?? r.CustomerName, r.Phone is null ? null : PhoneNumber.Mask(r.Phone), b.Note,
                b.LateCancellation, b.CancelledByVenue, Local(b.CreatedAt));
        }).ToList();
    }

    /// Staff timing rules shared by booking, blocking and moving. Returns an error sentence or null.
    private static async Task<string?> TimeErrorAsync(
        MidangoDb db, Venue venue, DateTimeOffset start, int minutes, DateTimeOffset now, CancellationToken ct)
    {
        if (minutes < 30 || minutes > 720 || minutes % 30 != 0)
            return "Choose a length in steps of 30 minutes, up to 12 hours.";
        // Range checks come first: an absurd start (year 9999) would overflow the time-zone conversion.
        if (start > now.AddDays(StaffMaxDaysAhead)) return $"Book within the next {StaffMaxDaysAhead} days.";
        if (start.AddMinutes(minutes) <= now) return "That time has already passed.";
        var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venue.Id).ToListAsync(ct);
        return Slots.Bookable(VenueClock.ToLocal(start, VenueClock.Zone(venue)), minutes, hours)
            ? null
            : "Pick a start on the half-hour grid inside opening hours, ending by closing time.";
    }

    /// A search term as a phone: the trailing digits staff typed (4+), and the full number when it parses.
    internal static (string? Tail, string? Exact) PhoneTerm(string term) => (
        term.Length >= 4 && term.All(char.IsAsciiDigit) ? term : null,
        PhoneNumber.TryParse(term, out var phone) ? phone.E164 : null);

    private static string? NoteError(string? note) =>
        note is { Length: > 200 } ? "Keep the note under 200 characters." : null;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// Escapes LIKE wildcards in user input (PostgreSQL's default escape character is the backslash).
    internal static string Like(string term) => term.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
