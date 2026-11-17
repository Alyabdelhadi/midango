# M6a Venue Operations API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Venue staff can run their day through the API: see the court × time schedule for a business day, book for a customer (by phone) or a walk-in (name only), block time on one or more courts, move a booking, cancel it, list and search bookings, see their customers with masked phones, and read the "Today" numbers. Managers set the free-cancellation window.

**Architecture:**
- **One table for everything on the grid:** staff bookings and blocked time are rows in `bookings`. A block is `kind = 'Block'` with no customer and a zero price. The M5 exclusion constraint (confirmed rows only) then stops bookings and blocks from overlapping with no new code, and the public availability already treats blocks as busy.
- **Who the booking is for:** a registered customer (`customer_id`), or a walk-in (`guest_name`), never both; blocks have neither. A database check constraint enforces this.
- **Staff rules differ from the app:** staff can book any length that is a multiple of 30 minutes (not only the sport's lengths), up to 90 days ahead, and a booking that started already but has not ended (a walk-in recorded late). Start times still follow the opening hours' 30-minute grid (`Slots.Bookable`).
- **Moving keeps the agreed price.** Only the court and start change; the price snapshot stays.
- **Business day:** the schedule, the list's date filters and Today use the opening period (16:00 → 02:00 next day). A closed day falls back to the calendar day.
- **Privacy (Q2):** staff only ever get `phoneMasked` ("•••• 4567"). Staff may type a full number to book for or look up a customer they are talking to. The API never returns the number.

**Tech Stack:** .NET 10 minimal API · EF Core 10 / Npgsql · PostgreSQL 18

**Project rules:** No automated tests; Task 4 is a scripted run against the live API. PowerShell. .NET packages stay on 10.x. Errors are `ApiErrors.Fail(status, sentence)`.

**Decisions this plan locks in:**

| Topic | Decision | Why |
|---|---|---|
| Blocks | Rows in `bookings` with `kind = 'Block'`; one row per court; removing a block = cancelling it | Reuses the exclusion constraint and availability |
| Staff length | Multiple of 30, 30–720 min, must fit the business day | Walk-ins ask for 2 h on a 90-minute sport |
| Staff window | Ends after now; starts within 90 days | Record walk-ins already playing; book regulars further ahead |
| Booking by phone | Find the customer by number; create the account (with the typed name) if new. An existing customer's name is never overwritten | Customer later signs in with that number and sees the booking |
| Walk-in without phone | `guest_name` only, no account | Desk speed |
| Move | Same sport only, same length, price kept, free-cancellation time recalculated | Venue courtesy; no surprise charges |
| Staff cancel | Any confirmed booking that hasn't ended; `cancelledByVenue = true`, never "late" | A venue cancellation never counts against the customer (M9) |
| Customers list | Only customers who booked at this venue; walk-ins without phone are not customers | The venue's own relationship |
| Phone lookup | Only finds customers with a booking at this venue | Staff can't probe the whole platform's numbers for names |
| Who | Manager and desk staff for everything here, except the free-cancellation setting (manager, via `PUT /venue`) | Desk runs the counter |

## Endpoints delivered

All under the `venue-staff` policy; the venue is always the caller's own (`venue_id` claim).

| Method & path | Result |
|---|---|
| `GET /venue/schedule?date=YYYY-MM-DD` | `{date, open, from, to, currency, courts[{id, categoryId, nameEn, nameAr, durationsMinutes[]}], bookings[StaffBooking]}` (confirmed bookings and blocks in the window) |
| `POST /venue/bookings` `{courtId, start, minutes, name, phone?, note?}` | 201 StaffBooking · 400 · 404 court · 409 taken |
| `POST /venue/blocks` `{courtIds[], start, minutes, note?}` | 201 StaffBooking[] · 400 · 404 · 409 (all-or-nothing) |
| `PUT /venue/bookings/{id}/move` `{courtId, start}` | 200 StaffBooking · 400 · 404 · 409 taken/finished |
| `POST /venue/bookings/{id}/cancel` | 200 StaffBooking (idempotent) · 404 · 409 finished |
| `GET /venue/bookings?from=&to=&status=&q=` | StaffBooking[] (kind Booking only, by start, max 500) · 400 range > 62 days |
| `GET /venue/customers?q=` | `[{id, name, phoneMasked, bookings, cancellations, lastBookingAt}]` (max 200) |
| `GET /venue/customers/lookup?phone=` | `{id, name, phoneMasked}` · 400 bad number · 404 |
| `GET /venue/today` | `{date, open, from, to, bookings, bookedMinutes, occupancyPercent, cashExpected, currency, cancelled, next[StaffBooking ≤ 5]}` |
| `PUT /venue` gains optional `freeCancellationHours` (0–168); `GET /venue` returns it | manager |

`StaffBooking` = `{id, reference, kind, source, courtId, courtNameEn, courtNameAr, startsAt, endsAt, minutes, total, currency, status, customerId, customerName, phoneMasked, note, lateCancellation, cancelledByVenue, createdAt}`. Times carry the venue's offset. `customerName` is the account name or the walk-in's name; `phoneMasked` is null for walk-ins and blocks.

## File structure

- Modify `api/src/Midango.Api/Bookings/Booking.cs`: kind, source, nullable customer, guest name, note, venue cancellation, settable court/times.
- Modify `api/src/Midango.Api/Bookings/Slots.cs`: add `Slots.DayWindow` and `Slots.BusinessDay`.
- Modify `api/src/Midango.Api/Bookings/BookingEndpoints.cs`: shared `BookingStore.SaveAsync`; customer DTO gains `cancelledByVenue`.
- Create `api/src/Midango.Api/Bookings/StaffBookingEndpoints.cs`: schedule, create, block, move, cancel, list, plus the `StaffBookingDto` loader.
- Create `api/src/Midango.Api/Bookings/VenueInsightEndpoints.cs`: customers, lookup, today.
- Modify `api/src/Midango.Api/Identity/PhoneNumber.cs`: `PhoneNumber.Mask(string e164)`.
- Modify `api/src/Midango.Api/Catalog/VenueEndpoints.cs`: free-cancellation hours in input and DTO.
- Modify `api/src/Midango.Api/Data/MidangoDb.cs`: new columns, check constraint, index.
- Create migration `StaffBookings`.
- Modify `api/src/Midango.Api/Program.cs`: map the two new endpoint groups.

---

### Task 1: Model, migration, shared save, venue setting

**Files:** `Bookings/Booking.cs`, `Bookings/BookingEndpoints.cs`, `Bookings/Slots.cs`, `Identity/PhoneNumber.cs`, `Catalog/VenueEndpoints.cs`, `Data/MidangoDb.cs`, new migration.

- [ ] **Step 1: Booking model.** In `Booking.cs`, add the enums above `Booking`:

```csharp
public enum BookingKind
{
    Booking,
    /// Time taken off sale by the venue (maintenance, private event). No customer, no price.
    Block,
}

public enum BookingSource
{
    App,
    Staff,
}
```

and change/add these members of `Booking` (others stay as they are):

```csharp
    public Guid CourtId { get; set; }

    /// Null for walk-ins without a phone and for blocks.
    public Guid? CustomerId { get; init; }

    /// Walk-in booked at the desk without a phone number. Never set together with CustomerId.
    public string? GuestName { get; init; }

    public BookingKind Kind { get; init; } = BookingKind.Booking;
    public BookingSource Source { get; init; } = BookingSource.App;
    public Guid? CreatedByStaffId { get; init; }

    /// Staff note (walk-in detail, block reason). Never shown to the customer.
    public string? Note { get; set; }

    /// UTC instants. Npgsql only writes offset-0 values to timestamptz.
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
```

```csharp
    /// Cancelling after this instant is allowed but marked late.
    public DateTimeOffset FreeCancellationUntil { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public bool LateCancellation { get; set; }

    /// Cancelled by the venue; never counts against the customer.
    public bool CancelledByVenue { get; set; }
```

- [ ] **Step 2: Mapping.** In `MidangoDb.cs`, in the `Booking` block replace the `ToTable` line and add the new properties and index. Keep every other existing line of that block (the `Customer` foreign key stays; EF makes it optional because `CustomerId` is now nullable).

```csharp
            e.ToTable("bookings", t =>
            {
                t.HasCheckConstraint("ck_bookings_ends_after_start", "ends_at > starts_at");
                // A booking is for exactly one of: an account or a walk-in. A block is for nobody.
                t.HasCheckConstraint("ck_bookings_party",
                    "(kind = 'Block' AND customer_id IS NULL AND guest_name IS NULL) OR " +
                    "(kind = 'Booking' AND (customer_id IS NULL) <> (guest_name IS NULL))");
            });
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.GuestName).HasMaxLength(60);
            e.Property(x => x.Note).HasMaxLength(200);
            e.HasIndex(x => new { x.VenueId, x.StartsAt });
```

- [ ] **Step 3: Migration.**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango\api
dotnet ef migrations add StaffBookings --project src/Midango.Api
```

Open the generated `*_StaffBookings.cs`. In `Up`, the `AddColumn<string>` calls for `kind` and `source` must have defaults that describe the existing rows: change their `defaultValue: ""` to `defaultValue: "Booking"` (kind) and `defaultValue: "App"` (source). The `AddCheckConstraint("ck_bookings_party", …)` must come **after** those `AddColumn` calls; move it down if EF placed it earlier. Do not touch the M5 exclusion constraint.

- [ ] **Step 4: Masking helper.** In `PhoneNumber.cs` replace the `Masked` property with:

```csharp
    /// "•••• 4567": the only form venue staff ever see (decision Q2).
    public string Masked => Mask(E164);

    public static string Mask(string e164) => $"•••• {e164[^4..]}";
```

- [ ] **Step 5: Business day helpers.** Append to the `Slots` class in `Slots.cs`:

```csharp
    /// The local window a "day" covers on the schedule, list and Today: the opening period,
    /// or the calendar day when the venue is closed.
    public static (DateTime From, DateTime To) DayWindow(DateOnly day, IReadOnlyList<OpeningHours> hours) =>
        Period(day, hours) ?? (day.ToDateTime(TimeOnly.MinValue), day.ToDateTime(TimeOnly.MinValue).AddDays(1));

    /// The business day a local moment belongs to: 01:00 is still "yesterday" while yesterday's period runs.
    public static DateOnly BusinessDay(DateTime local, IReadOnlyList<OpeningHours> hours)
    {
        var day = DateOnly.FromDateTime(local);
        return Period(day.AddDays(-1), hours) is { } p && local < p.Closes ? day.AddDays(-1) : day;
    }
```

- [ ] **Step 6: Shared save.** In `BookingEndpoints.cs`, add this class at the end of the file (same namespace), and make the customer `POST /bookings` use it: replace the whole `for (var attempt …)` loop with `if (await BookingStore.SaveAsync(db, [booking], ct) is { } failed) return failed;`.

```csharp
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
            catch (DbUpdateException ex) when (attempt < 3 && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                foreach (var b in added) b.Reference = BookingReference.New();
            }
        }
    }
}
```

Also in that file: set `Kind = BookingKind.Booking, Source = BookingSource.App` explicitly in the customer `new Booking { … }`; add `bool CancelledByVenue` as the last parameter of `BookingDto` and pass `b.CancelledByVenue` in its `LoadAsync`.

- [ ] **Step 7: Free-cancellation setting.** In `VenueEndpoints.cs`:
  - `VenueInput` gains a last parameter `int? FreeCancellationHours`.
  - `VenueDto` gains `int FreeCancellationHours` right after `decimal VatPercent`; pass `venue.FreeCancellationHours` in `LoadAsync`.
  - `ValidateVenue` adds, before `return null;`:
    ```csharp
        if (input.FreeCancellationHours is < 0 or > 168)
            return "Free cancellation must be between 0 and 168 hours before the start.";
    ```
  - In `PUT /venue`, after the time-zone line: `venue.FreeCancellationHours = input.FreeCancellationHours ?? venue.FreeCancellationHours;`

  Existing callers that leave the field out keep the current value.

- [ ] **Step 8: Build and migrate.**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango\api
dotnet build
dotnet ef database update --project src/Midango.Api
```

Expected: build 0 warnings 0 errors; migration applied. Check that the existing rows took the defaults:

```powershell
$env:PGPASSWORD = 'midango_dev'
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U midango -h localhost -d midango -c "SELECT kind, source, count(*) FROM bookings GROUP BY 1,2;"
Remove-Item Env:PGPASSWORD
```

Expected: only `Booking | App` rows.

- [ ] **Step 9: Commit** `feat(api): bookings carry kind, source, walk-in name and note; venue free-cancellation setting`.

---

### Task 2: Staff bookings — schedule, create, block, move, cancel, list

**Files:** Create `Bookings/StaffBookingEndpoints.cs`; modify `Program.cs`.

- [ ] **Step 1: Write the file.**

```csharp
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
            var local = VenueClock.ToLocal(start, VenueClock.Zone(venue));
            if (await TimeErrorAsync(db, venue.Id, local, start, body.Minutes, now, ct) is { } timeError)
                return ApiErrors.Fail(400, timeError);

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
                GuestName = hasPhone ? null : name,
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
            var local = VenueClock.ToLocal(start, VenueClock.Zone(venue));
            if (await TimeErrorAsync(db, venue.Id, local, start, body.Minutes, now, ct) is { } timeError)
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
            var local = VenueClock.ToLocal(start, VenueClock.Zone(venue));
            if (await TimeErrorAsync(db, venue.Id, local, start, booking.Minutes, now, ct) is { } timeError)
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
                var phoneTail = term.Length >= 4 && term.All(char.IsAsciiDigit) ? term : null;
                query = query.Where(b =>
                    b.Reference == reference
                    || (b.GuestName != null && EF.Functions.ILike(b.GuestName, pattern))
                    || db.Customers.Any(c => c.Id == b.CustomerId
                        && ((c.Name != null && EF.Functions.ILike(c.Name, pattern))
                            || (phoneTail != null && c.PhoneE164.EndsWith(phoneTail)))));
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
                b.CustomerId, r.CustomerName ?? b.GuestName, r.Phone is null ? null : PhoneNumber.Mask(r.Phone), b.Note,
                b.LateCancellation, b.CancelledByVenue, Local(b.CreatedAt));
        }).ToList();
    }

    /// Staff timing rules shared by booking, blocking and moving. Returns an error sentence or null.
    private static async Task<string?> TimeErrorAsync(
        MidangoDb db, Guid venueId, DateTime local, DateTimeOffset start, int minutes, DateTimeOffset now, CancellationToken ct)
    {
        if (minutes < 30 || minutes > 720 || minutes % 30 != 0)
            return "Choose a length in steps of 30 minutes, up to 12 hours.";
        if (start.AddMinutes(minutes) <= now) return "That time has already passed.";
        if (start > now.AddDays(StaffMaxDaysAhead)) return $"Book within the next {StaffMaxDaysAhead} days.";
        var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venueId).ToListAsync(ct);
        return Slots.Bookable(local, minutes, hours)
            ? null
            : "Pick a start on the half-hour grid inside opening hours, ending by closing time.";
    }

    private static string? NoteError(string? note) =>
        note is { Length: > 200 } ? "Keep the note under 200 characters." : null;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// Escapes LIKE wildcards in user input (PostgreSQL's default escape character is the backslash).
    internal static string Like(string term) => term.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
```

- [ ] **Step 2: Map it.** In `Program.cs` add `app.MapStaffBookingEndpoints();` after `app.MapBookingEndpoints();`.

- [ ] **Step 3: Build.** `dotnet build` in `api/` → 0 warnings, 0 errors. If EF can't translate a query at runtime it only shows in Task 4; keep the shape and fix the translation there.

- [ ] **Step 4: Commit** `feat(api): venue schedule, staff and walk-in bookings, blocked time, move, cancel and search`.

---

### Task 3: Customers, lookup and Today

**Files:** Create `Bookings/VenueInsightEndpoints.cs`; modify `Program.cs`.

- [ ] **Step 1: Write the file.**

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Bookings;

public static class VenueInsightEndpoints
{
    public sealed record VenueCustomerDto(
        Guid Id, string? Name, string PhoneMasked, int Bookings, int Cancellations, DateTimeOffset LastBookingAt);

    public sealed record CustomerLookupDto(Guid Id, string? Name, string PhoneMasked);

    public sealed record TodayDto(
        DateOnly Date, bool Open, DateTimeOffset From, DateTimeOffset To,
        int Bookings, int BookedMinutes, int OccupancyPercent, decimal CashExpected, string Currency, int Cancelled,
        IReadOnlyList<StaffBookingEndpoints.StaffBookingDto> Next);

    public static void MapVenueInsightEndpoints(this IEndpointRouteBuilder app)
    {
        var venueStaff = app.MapGroup("/venue").RequireAuthorization(Policies.VenueStaff);

        venueStaff.MapGet("/customers", async (string? q, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var stats = db.Bookings
                .Where(b => b.VenueId == venueId && b.Kind == BookingKind.Booking && b.CustomerId != null)
                .GroupBy(b => b.CustomerId!.Value)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Bookings = g.Count(b => b.Status == BookingStatus.Confirmed),
                    Cancellations = g.Count(b => b.Status == BookingStatus.Cancelled),
                    Last = g.Max(b => b.StartsAt),
                });
            var rows = from s in stats
                       join c in db.Customers on s.CustomerId equals c.Id
                       select new { c.Id, c.Name, c.PhoneE164, s.Bookings, s.Cancellations, s.Last };

            var term = q?.Trim();
            if (!string.IsNullOrEmpty(term))
            {
                var pattern = $"%{StaffBookingEndpoints.Like(term)}%";
                var phoneTail = term.Length >= 4 && term.All(char.IsAsciiDigit) ? term : null;
                rows = rows.Where(r => (r.Name != null && EF.Functions.ILike(r.Name, pattern))
                                       || (phoneTail != null && r.PhoneE164.EndsWith(phoneTail)));
            }

            var list = await rows.OrderByDescending(r => r.Last).Take(200).AsNoTracking().ToListAsync(ct);
            return Results.Ok(list.Select(r => new VenueCustomerDto(
                r.Id, r.Name, PhoneNumber.Mask(r.PhoneE164), r.Bookings, r.Cancellations, r.Last)));
        });

        venueStaff.MapGet("/customers/lookup", async (string? phone, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            if (!PhoneNumber.TryParse(phone, out var parsed)) return ApiErrors.Fail(400, "Enter a valid mobile number.");
            var venueId = user.StaffVenueId()!.Value;
            var customer = await db.Customers.AsNoTracking()
                .Where(c => c.PhoneE164 == parsed.E164
                            && db.Bookings.Any(b => b.CustomerId == c.Id && b.VenueId == venueId))
                .Select(c => new { c.Id, c.Name, c.PhoneE164 })
                .SingleOrDefaultAsync(ct);
            return customer is null
                ? ApiErrors.Fail(404, "No customer with that number has booked here yet.")
                : Results.Ok(new CustomerLookupDto(customer.Id, customer.Name, PhoneNumber.Mask(customer.PhoneE164)));
        });

        venueStaff.MapGet("/today", async (ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);
            var zone = VenueClock.Zone(venue);
            var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venue.Id).ToListAsync(ct);
            var now = clock.GetUtcNow();
            var day = Slots.BusinessDay(VenueClock.ToLocal(now, zone), hours);
            var period = Slots.Period(day, hours);
            var (fromLocal, toLocal) = Slots.DayWindow(day, hours);
            var from = VenueClock.ToInstant(fromLocal, zone).ToUniversalTime();
            var to = VenueClock.ToInstant(toLocal, zone).ToUniversalTime();

            var inWindow = db.Bookings.Where(b => b.VenueId == venue.Id && b.Kind == BookingKind.Booking
                                                  && b.StartsAt >= from && b.StartsAt < to);
            var confirmed = await inWindow.Where(b => b.Status == BookingStatus.Confirmed)
                .Select(b => new { b.Minutes, b.Total }).ToListAsync(ct);
            var cancelled = await inWindow.CountAsync(b => b.Status == BookingStatus.Cancelled, ct);
            var courts = await db.Courts.CountAsync(c => c.VenueId == venue.Id && c.Active, ct);

            var bookedMinutes = confirmed.Sum(b => b.Minutes);
            var openMinutes = period is { } p ? (int)(p.Closes - p.Opens).TotalMinutes * courts : 0;
            var occupancy = openMinutes == 0 ? 0 : (int)Math.Round(100.0 * bookedMinutes / openMinutes);

            var next = await StaffBookingEndpoints.LoadAsync(db, venue, inWindow
                .Where(b => b.Status == BookingStatus.Confirmed && b.EndsAt > now)
                .OrderBy(b => b.StartsAt).Take(5), ct);

            return Results.Ok(new TodayDto(
                day, period is not null, TimeZoneInfo.ConvertTime(from, zone), TimeZoneInfo.ConvertTime(to, zone),
                confirmed.Count, bookedMinutes, Math.Min(occupancy, 100), confirmed.Sum(b => b.Total),
                venue.CurrencyCode, cancelled, next));
        });
    }
}
```

- [ ] **Step 2: Map it** in `Program.cs`: `app.MapVenueInsightEndpoints();` after `app.MapStaffBookingEndpoints();`.

- [ ] **Step 3: Build** → 0 warnings, 0 errors.

- [ ] **Step 4: Commit** `feat(api): venue customers with masked phones, phone lookup and Today numbers`.

---

### Task 4: Verify against the running API (controller)

- [ ] **Step 1: Start the API with its log captured** (as in M2 Task 5).

- [ ] **Step 2: Run this script (outside the repo, `pwsh -NoProfile -File`)**

```powershell
$api = 'http://localhost:5300'
$log = "$env:TEMP\midango-api.log"
$script:failures = 0

function Call([string]$Method, [string]$Path, $Body, [string]$Token, [int]$Expect) {
    $p = @{ Uri = "$api$Path"; Method = $Method; SkipHttpErrorCheck = $true; ContentType = 'application/json' }
    if ($null -ne $Body) { $p.Body = ConvertTo-Json $Body -Compress -Depth 6 }
    if ($Token) { $p.Headers = @{ Authorization = "Bearer $Token" } }
    $r = Invoke-WebRequest @p
    $text = [string]$r.Content
    $ok = [int]$r.StatusCode -eq $Expect
    if (-not $ok) { $script:failures++ }
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    $short = if ($text.Length -gt 140) { $text.Substring(0, 140) + '...' } else { $text }
    Write-Host "$mark $Method $Path -> $([int]$r.StatusCode) (expected $Expect) $short"
    if ($text) { try { return $text | ConvertFrom-Json -DateKind String } catch { return $null } }
}
function Check([bool]$Condition, [string]$What) {
    if ($Condition) { Write-Host "PASS $What" } else { $script:failures++; Write-Host "FAIL $What" }
}
function SignInCustomer([string]$Local, [string]$Name) {
    Call POST /auth/otp/request @{ phone = $Local } -Expect 202 | Out-Null
    Start-Sleep -Milliseconds 800
    $code = (Select-String -Path $log -Pattern 'DEV OTP for .*: (\d{6})' | Select-Object -Last 1).Matches[0].Groups[1].Value
    $token = (Call POST /auth/otp/verify @{ phone = $Local; code = $code } -Expect 200).accessToken
    if ($Name) { Call PUT /me @{ name = $Name } -Token $token -Expect 200 | Out-Null }
    return $token
}
function NewPhone { return '055' + (Get-Random -Minimum 1000000 -Maximum 9999999) }

$day = [DateTime]::UtcNow.AddHours(3).Date.AddDays(2)
$date = $day.ToString('yyyy-MM-dd')
$next = $day.AddDays(1).ToString('yyyy-MM-dd')
function At([string]$Date, [string]$Time) { return "${Date}T${Time}:00+03:00" }

# Setup: published venue 16:00–02:00, two padel courts and one football pitch, a manager and a desk user.
$op = (Call POST /staff/auth/login @{ email = 'operator@midango.local'; password = 'Operator!2026dev' } -Expect 200).accessToken
$s = Get-Random
$padel = Call POST /admin/categories @{ nameEn = "Padel $s"; nameAr = 'بادل'; iconKey = 'volleyball'; durationsMinutes = @(60, 90); sortOrder = 1; active = $true } -Token $op -Expect 201
$foot = Call POST /admin/categories @{ nameEn = "Foot $s"; nameAr = 'كرة'; iconKey = 'goal'; durationsMinutes = @(60); sortOrder = 2; active = $true } -Token $op -Expect 201
$v = Call POST /admin/venues @{ nameEn = "Ops $s"; nameAr = 'تشغيل' } -Token $op -Expect 201
Call POST "/admin/venues/$($v.id)/staff" @{ email = "ops-$s@arena.test"; displayName = 'Mgr'; role = 'VenueManager'; temporaryPassword = 'Temp-pass-123' } -Token $op -Expect 201 | Out-Null
Call POST "/admin/venues/$($v.id)/staff" @{ email = "desk-$s@arena.test"; displayName = 'Desk'; role = 'DeskStaff'; temporaryPassword = 'Temp-pass-123' } -Token $op -Expect 201 | Out-Null
$mgr = (Call POST /staff/auth/login @{ email = "ops-$s@arena.test"; password = 'Temp-pass-123' } -Expect 200).accessToken
$desk = (Call POST /staff/auth/login @{ email = "desk-$s@arena.test"; password = 'Temp-pass-123' } -Expect 200).accessToken
$details = @{ nameEn = "Ops $s"; nameAr = 'تشغيل'; addressEn = 'Riyadh'; addressAr = 'الرياض'; latitude = 24.7; longitude = 46.7 }
Call PUT /venue $details -Token $mgr -Expect 200 | Out-Null
$week = 'Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday' | ForEach-Object { @{ day = $_; opens = '16:00:00'; closes = '02:00:00' } }
Call PUT /venue/hours $week -Token $mgr -Expect 200 | Out-Null
Call POST /venue/courts @{ categoryId = $padel.id; nameEn = 'Court 1'; nameAr = 'ملعب 1'; indoor = $true; hourlyPrice = 150; featureIds = @(); sortOrder = 1; active = $true } -Token $mgr -Expect 201 | Out-Null
Call POST /venue/courts @{ categoryId = $padel.id; nameEn = 'Court 2'; nameAr = 'ملعب 2'; indoor = $true; hourlyPrice = 150; featureIds = @(); sortOrder = 2; active = $true } -Token $mgr -Expect 201 | Out-Null
$venue = Call POST /venue/courts @{ categoryId = $foot.id; nameEn = 'Pitch'; nameAr = 'ملعب كرة'; indoor = $false; hourlyPrice = 300; featureIds = @(); sortOrder = 3; active = $true } -Token $mgr -Expect 201
$c1 = $venue.courts[0].id; $c2 = $venue.courts[1].id; $pitch = $venue.courts[2].id
Call POST /venue/publish -Token $mgr -Expect 200 | Out-Null

# 1. Free-cancellation setting
Check ($venue.freeCancellationHours -eq 24) 'venue starts with 24 h free cancellation'
Call PUT /venue ($details + @{ freeCancellationHours = 200 }) -Token $mgr -Expect 400 | Out-Null
$updated = Call PUT /venue ($details + @{ freeCancellationHours = 12 }) -Token $mgr -Expect 200
Check ($updated.freeCancellationHours -eq 12) 'manager sets free cancellation to 12 h'
$kept = Call PUT /venue $details -Token $mgr -Expect 200
Check ($kept.freeCancellationHours -eq 12) 'leaving it out keeps 12 h'
Call PUT /venue ($details + @{ freeCancellationHours = 6 }) -Token $desk -Expect 403 | Out-Null

# 2. Staff bookings: by phone (new account), walk-in, rules
$phone = NewPhone
$tail = $phone.Substring($phone.Length - 4)
$b1 = Call POST /venue/bookings @{ courtId = $c1; start = (At $date '20:00'); minutes = 120; name = 'Omar'; phone = $phone; note = 'Regular' } -Token $desk -Expect 201
Check ($b1.source -eq 'Staff' -and $b1.kind -eq 'Booking' -and $b1.minutes -eq 120 -and $b1.total -eq 300) 'desk books 120 min (not a padel length) for 300.00'
Check ($b1.customerName -eq 'Omar' -and $b1.phoneMasked -eq "•••• $tail" -and $b1.note -eq 'Regular') 'customer name and masked phone, note kept'
Check ($b1.startsAt -like "*T20:00:00+03:00") 'staff booking shown in venue time'
$raw = (Invoke-WebRequest -Uri "$api/venue/bookings?from=$date" -Headers @{ Authorization = "Bearer $desk" }).Content
Check (-not $raw.Contains($phone.Substring(1))) 'full phone number never appears in the staff response'
$walk = Call POST /venue/bookings @{ courtId = $c2; start = (At $date '20:00'); minutes = 60; name = 'Walk-in Khalid' } -Token $desk -Expect 201
Check ($null -eq $walk.customerId -and $walk.customerName -eq 'Walk-in Khalid' -and $null -eq $walk.phoneMasked) 'walk-in without phone: name only'
Call POST /venue/bookings @{ courtId = $c1; start = (At $date '21:00'); minutes = 60; name = 'Clash' } -Token $desk -Expect 409 | Out-Null
Call POST /venue/bookings @{ courtId = $c1; start = (At $date '18:00'); minutes = 45; name = 'Odd' } -Token $desk -Expect 400 | Out-Null
Call POST /venue/bookings @{ courtId = $c1; start = (At $date '18:15'); minutes = 60; name = 'Grid' } -Token $desk -Expect 400 | Out-Null
Call POST /venue/bookings @{ courtId = $c1; start = (At $next '01:30'); minutes = 60; name = 'Late' } -Token $desk -Expect 400 | Out-Null
Call POST /venue/bookings @{ courtId = $c1; start = (At $date '18:00'); minutes = 60; name = '' } -Token $desk -Expect 400 | Out-Null
Call POST /venue/bookings @{ courtId = $c1; start = (At $date '18:00'); minutes = 60; name = 'X'; phone = '123' } -Token $desk -Expect 400 | Out-Null
Call POST /venue/bookings @{ courtId = $c1; start = (At $date '18:00'); minutes = 60; name = 'X' } -Token $op -Expect 403 | Out-Null

# The account created by phone is the customer's own: they sign in and see it, and their own name wins later.
$omar = SignInCustomer $phone $null
$mine = Call GET /me/bookings -Token $omar -Expect 200
Check ($mine.upcoming.Count -eq 1 -and $mine.upcoming[0].reference -eq $b1.reference) 'customer signs in with that number and sees the desk booking'
Call PUT /me @{ name = 'Omar A.' } -Token $omar -Expect 200 | Out-Null
$again = Call POST /venue/bookings @{ courtId = $c2; start = (At $date '22:00'); minutes = 60; name = 'Someone else'; phone = $phone } -Token $desk -Expect 201
Check ($again.customerName -eq 'Omar A.') "an existing customer's own name is never overwritten"

# 3. Blocks
$blocks = @(Call POST /venue/blocks @{ courtIds = @($c1, $c2); start = (At $date '16:00'); minutes = 120; note = 'Resurfacing' } -Token $desk -Expect 201)
Check ($blocks.Count -eq 2 -and $blocks[0].kind -eq 'Block' -and $blocks[0].total -eq 0 -and $null -eq $blocks[0].customerName) 'two courts blocked 16:00–18:00, no customer, no price'
Call POST /venue/blocks @{ courtIds = @($pitch, $c1); start = (At $date '19:00'); minutes = 120 } -Token $desk -Expect 409 | Out-Null
$sched = Call GET "/venue/schedule?date=$date" -Token $desk -Expect 200
Check (@($sched.bookings | Where-Object courtId -eq $pitch).Count -eq 0) 'all-or-nothing: the free pitch was not blocked either'
$a = Call GET "/venues/$($v.id)/availability?date=$date&categoryId=$($padel.id)&minutes=60" -Expect 200
Check (($a.slots | Where-Object localTime -eq '16:00').courts.Count -eq 0) 'blocked time is gone from the app'
Call POST /venue/blocks @{ courtIds = @(); start = (At $date '16:00'); minutes = 60 } -Token $desk -Expect 400 | Out-Null

# 4. Schedule
Check ($sched.open -and $sched.from -like "${date}T16:00:00+03:00" -and $sched.to -like "${next}T02:00:00+03:00") 'schedule window is the business day 16:00 → 02:00'
Check ($sched.courts.Count -eq 3 -and $sched.courts[0].durationsMinutes.Count -eq 2) 'three courts with their sport lengths'
Check ($sched.bookings.Count -eq 5) 'two blocks + three bookings on the grid'

# 5. Move
$moved = Call PUT "/venue/bookings/$($walk.id)/move" @{ courtId = $c2; start = (At $date '23:00') } -Token $desk -Expect 200
Check ($moved.startsAt -like "*T23:00:00+03:00" -and $moved.total -eq $walk.total) 'moved to 23:00, price kept'
Call PUT "/venue/bookings/$($walk.id)/move" @{ courtId = $c1; start = (At $date '20:30') } -Token $desk -Expect 409 | Out-Null
Call PUT "/venue/bookings/$($walk.id)/move" @{ courtId = $pitch; start = (At $date '23:00') } -Token $desk -Expect 400 | Out-Null
Call PUT "/venue/bookings/$($walk.id)/move" @{ courtId = $c2; start = (At $date '23:10') } -Token $desk -Expect 400 | Out-Null
$blockMoved = Call PUT "/venue/bookings/$($blocks[0].id)/move" @{ courtId = $pitch; start = (At $date '16:00') } -Token $desk -Expect 200
Check ($blockMoved.courtId -eq $pitch) 'a block can move to any court'

# 6. Cancel by the venue
$app = SignInCustomer (NewPhone) 'Lina'
$appBooking = Call POST /bookings @{ courtId = $c1; start = (At $date '23:00'); minutes = 60 } -Token $app -Expect 201
Check ($appBooking.freeCancellationUntil -like "*T11:00:00+03:00") 'app booking uses the 12 h setting'
$vc = Call POST "/venue/bookings/$($appBooking.id)/cancel" -Token $desk -Expect 200
Check ($vc.status -eq 'Cancelled' -and $vc.cancelledByVenue -and -not $vc.lateCancellation) 'venue cancel: cancelledByVenue, never late'
$lina = Call GET /me/bookings -Token $app -Expect 200
Check ($lina.history[0].cancelledByVenue) 'customer sees it was cancelled by the venue'
Call POST "/venue/bookings/$($blocks[1].id)/cancel" -Token $desk -Expect 200 | Out-Null
$a = Call GET "/venues/$($v.id)/availability?date=$date&categoryId=$($padel.id)&minutes=60" -Expect 200
Check (($a.slots | Where-Object localTime -eq '16:00').courts.Count -eq 2) 'removing / moving the blocks frees 16:00 again'

# 7. Another venue's staff see nothing
$other = Call POST /admin/venues @{ nameEn = "Other $s"; nameAr = 'أخرى' } -Token $op -Expect 201
Call POST "/admin/venues/$($other.id)/staff" @{ email = "other-$s@arena.test"; displayName = 'O'; role = 'VenueManager'; temporaryPassword = 'Temp-pass-123' } -Token $op -Expect 201 | Out-Null
$otherMgr = (Call POST /staff/auth/login @{ email = "other-$s@arena.test"; password = 'Temp-pass-123' } -Expect 200).accessToken
Call POST "/venue/bookings/$($b1.id)/cancel" -Token $otherMgr -Expect 404 | Out-Null
Call PUT "/venue/bookings/$($b1.id)/move" @{ courtId = $c1; start = (At $date '18:00') } -Token $otherMgr -Expect 404 | Out-Null
$none = Call GET "/venue/bookings?from=$date" -Token $otherMgr -Expect 200
Check (@($none).Count -eq 0) "another venue's list is empty"
Call GET "/venue/customers/lookup?phone=$phone" -Token $otherMgr -Expect 404 | Out-Null

# 8. List and search
$list = @(Call GET "/venue/bookings?from=$date" -Token $desk -Expect 200)
Check ($list.Count -eq 4 -and @($list | Where-Object kind -eq 'Block').Count -eq 0) 'list: four bookings for the day, no blocks'
Check ($list[0].startsAt -le $list[-1].startsAt) 'sorted by start'
Check (@(Call GET "/venue/bookings?from=$date&status=Cancelled" -Token $desk -Expect 200).Count -eq 1) 'status filter'
Check (@(Call GET "/venue/bookings?from=$date&q=$tail" -Token $desk -Expect 200).Count -eq 2) 'search by last 4 digits'
Check (@(Call GET "/venue/bookings?from=$date&q=khal" -Token $desk -Expect 200).Count -eq 1) 'search walk-in name, case-insensitive'
Check (@(Call GET "/venue/bookings?from=$date&q=$($b1.reference.ToLower())" -Token $desk -Expect 200).Count -eq 1) 'search by reference'
Check (@(Call GET "/venue/bookings?from=$date&q=%25" -Token $desk -Expect 200).Count -eq 0) 'a % in the search is literal'
Call GET "/venue/bookings?from=$date&to=$($day.AddDays(70).ToString('yyyy-MM-dd'))" -Token $desk -Expect 400 | Out-Null

# 9. Customers and lookup
$cust = @(Call GET /venue/customers -Token $desk -Expect 200)
Check ($cust.Count -eq 2) 'two customers with accounts (the walk-in is not a customer)'
$omarRow = $cust | Where-Object name -eq 'Omar A.'
Check ($omarRow.bookings -eq 2 -and $omarRow.phoneMasked -eq "•••• $tail") 'Omar: 2 bookings, masked phone'
Check (@(Call GET "/venue/customers?q=lin" -Token $desk -Expect 200).Count -eq 1) 'customer search by name'
$found = Call GET "/venue/customers/lookup?phone=$phone" -Token $desk -Expect 200
Check ($found.name -eq 'Omar A.' -and $found.phoneMasked -eq "•••• $tail" -and -not ($found | ConvertTo-Json).Contains($phone.Substring(1))) 'lookup returns name and masked phone only'
Call GET "/venue/customers/lookup?phone=$(NewPhone)" -Token $desk -Expect 404 | Out-Null
Call GET "/venue/customers/lookup?phone=abc" -Token $desk -Expect 400 | Out-Null

# 10. Today
$today = Call GET /venue/today -Token $desk -Expect 200
Check ($today.open -and $today.currency -eq 'SAR' -and $today.from -like "*T16:00:00+03:00") 'today: open, SAR, window starts at opening'
Write-Host "INFO today = $($today.date), bookings $($today.bookings), occupancy $($today.occupancyPercent)%, cash $($today.cashExpected)"

Write-Host "`nFailures: $script:failures"
```

Expected: every line `PASS`, `Failures: 0`.

- [ ] **Step 3: Database checks.**

```powershell
$env:PGPASSWORD = 'midango_dev'
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U midango -h localhost -d midango -c "UPDATE bookings SET guest_name = 'X' WHERE id = (SELECT id FROM bookings WHERE customer_id IS NOT NULL LIMIT 1);"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U midango -h localhost -d midango -c "UPDATE bookings SET customer_id = (SELECT id FROM customers LIMIT 1) WHERE id = (SELECT id FROM bookings WHERE kind = 'Block' LIMIT 1);"
Remove-Item Env:PGPASSWORD
```

Expected: both fail with `violates check constraint "ck_bookings_party"`.

- [ ] **Step 4:** Wait 60 s (auth rate limit), re-run the M5 script (`Failures: 0`), stop the API, tick M6a in the roadmap, and commit.

---

## What M6a deliberately leaves out

- Price override / discounts at the desk. Add when a venue asks.
- Recurring bookings ("every Tuesday 20:00"). Add with the M7 desk feedback.
- Multi-day closures (Eid): block each day for now.
- Check-in and cash taken: M7.
- Who cancelled (staff id) is not stored; `cancelledByVenue` is enough for M9.
