# M5 Availability & Booking Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Customers can see which start times are open at a published venue for a sport and booking length (every time listed, with the free courts and their prices), book one with "pay at the venue", list their bookings, and cancel. Double bookings are impossible, even under concurrent requests.

**Architecture:**
- **Engine:** a new `Bookings/` feature folder. Pure helpers (`Slots`, `VenueClock`) turn opening hours (including past midnight) into candidate start times in the venue's time zone. The M4 `Pricing` calculator prices every free court.
- **Overlap safety:** bookings store UTC instants. A PostgreSQL **exclusion constraint** (`btree_gist`, `court_id =` and `tstzrange &&`, confirmed bookings only) is the final guard against overlapping bookings. The application maps its violation to a 409.
- **No hold step:** payment is cash at the venue (Q3/Q4), so confirming is one tap and nothing slow happens between seeing a time and booking it. A race simply ends with a clear 409. This replaces the roadmap's "short hold".
- **Cancellation:** cancelling before the venue's free-cancellation window is free. Later is still allowed but flagged `lateCancellation` (input for the M9 no-show rules). A booking that has started can't be cancelled.

**Tech Stack:** .NET 10 · EF Core 10 / Npgsql · PostgreSQL 18 `btree_gist` · M4 `Pricing`

**Project rules:** No automated tests; Task 4 is a scripted run against the live API, including a parallel booking race, plus database checks. PowerShell. .NET packages stay on 10.x.

**Decisions this plan locks in:**

| Topic | Decision | Why |
|---|---|---|
| Start times | Every 30 minutes from the opening time of the business day, while start + length ≤ closing. A business day that closes after midnight continues into the next calendar day | Matches how venues sell courts; works for 16:00–02:00 venues |
| Availability response | Every candidate time is listed, each with its free courts and each court's price. A time with no free court has an empty list | The app shows full times struck through (research) and prices only when they differ |
| Booking window | From now (past starts are hidden and refused) to 30 days ahead | Keeps the date strip short |
| Who can book | A signed-in customer with a name on their profile (409 otherwise). Staff-created bookings come in M6 | Desk staff identify customers by name (Q2) |
| Payment | `CashAtVenue`, nothing collected in the app; price snapshot (total, net, VAT, currency) stored on the booking | Q3/Q4; later price changes never alter a booking |
| Reference | 6 characters from `ABCDEFGHJKMNPQRSTUVWXYZ23456789` (no 0/O/1/I/L), unique | Readable aloud at the desk |
| Free cancellation | Per venue, default 24 hours before the start | Common padel policy; a venue setting arrives with the M6 settings screen |
| Time zones | Venue IANA zone; Asia/Riyadh has no DST. Ambiguous or skipped local times in DST zones are not handled yet | Correct for the launch market; noted for GCC expansion |

## Endpoints delivered

| Method & path | Who | Result |
|---|---|---|
| `GET /venues/{venueId}/availability?date=YYYY-MM-DD&categoryId=…&minutes=…` | anyone | `{venueId, date, categoryId, minutes, currency, open, slots[{start, localTime, courts[{courtId, total}]}]}` · 404 unpublished · 400 bad length/date |
| `POST /bookings` `{courtId, start, minutes}` | customer | 201 booking · 400 · 404 · 409 taken / no name |
| `GET /me/bookings` | customer | `{upcoming[], history[]}` |
| `POST /bookings/{id}/cancel` | customer (own) | 200 booking (idempotent) · 404 · 409 started |

## File structure

```
api/src/Midango.Api/
├── Program.cs                       modified: map the two new endpoint groups
├── Data/MidangoDb.cs                modified: Bookings, btree_gist, config
├── Data/Migrations/*_Bookings.cs    generated + hand-added exclusion constraint
├── Venues/Venue.cs                  modified: FreeCancellationHours
└── Bookings/
    ├── Booking.cs                   Booking, BookingStatus, BookingReference
    ├── Slots.cs                     VenueClock, Slots (pure)
    ├── AvailabilityEndpoints.cs
    └── BookingEndpoints.cs
```

---

### Task 1: Booking model, exclusion constraint, migration

**Files:**
- Create: `api/src/Midango.Api/Bookings/Booking.cs`
- Modify: `api/src/Midango.Api/Venues/Venue.cs`, `api/src/Midango.Api/Data/MidangoDb.cs`
- Create: migration `Bookings` (generated, then edited)

- [ ] **Step 1: Create `Bookings/Booking.cs`**

```csharp
using System.Security.Cryptography;

namespace Midango.Api.Bookings;

public enum BookingStatus
{
    Confirmed,
    Cancelled,
}

public sealed class Booking
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// Short code read out at the desk, e.g. "K7Q2MX".
    public required string Reference { get; set; }
    public Guid VenueId { get; init; }
    public Guid CourtId { get; init; }
    public Guid CustomerId { get; init; }

    /// UTC instants. Npgsql only writes offset-0 values to timestamptz.
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public int Minutes { get; init; }

    /// Price snapshot at booking time, VAT-inclusive. Later price changes never touch it.
    public decimal Total { get; init; }
    public decimal Net { get; init; }
    public decimal Vat { get; init; }
    public required string Currency { get; init; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTimeOffset CreatedAt { get; init; }

    /// Cancelling after this instant is allowed but marked late.
    public DateTimeOffset FreeCancellationUntil { get; init; }
    public DateTimeOffset? CancelledAt { get; set; }
    public bool LateCancellation { get; set; }
}

public static class BookingReference
{
    // No 0/O or 1/I/L: the code is read aloud and typed at the desk.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string New() => string.Create(6, 0, static (span, _) =>
    {
        for (var i = 0; i < span.Length; i++) span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
    });
}
```

- [ ] **Step 2: Add the cancellation window to `Venues/Venue.cs`**

After `VatPercent`, add:

```csharp
    /// Customers cancel for free until this many hours before the start; later counts as a late cancellation.
    public int FreeCancellationHours { get; set; } = 24;
```

- [ ] **Step 3: Configure in `Data/MidangoDb.cs`**

Add `using Midango.Api.Bookings;`, the DbSet:

```csharp
    public DbSet<Booking> Bookings => Set<Booking>();
```

At the very top of `OnModelCreating`, add:

```csharp
        b.HasPostgresExtension("btree_gist");
```

And a new block after the `PriceBand` block (before the Guid-key loop):

```csharp
        b.Entity<Booking>(e =>
        {
            // The no-overlap exclusion constraint is added by hand in the Bookings migration (EF can't express it).
            e.ToTable("bookings", t => t.HasCheckConstraint("ck_bookings_ends_after_start", "ends_at > starts_at"));
            e.Property(x => x.Reference).HasMaxLength(12);
            e.HasIndex(x => x.Reference).IsUnique();
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Total).HasPrecision(10, 2);
            e.Property(x => x.Net).HasPrecision(10, 2);
            e.Property(x => x.Vat).HasPrecision(10, 2);
            e.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CourtId, x.StartsAt });
            e.HasIndex(x => new { x.CustomerId, x.StartsAt });
        });
```

- [ ] **Step 4: Generate the migration and add the constraint by hand**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
dotnet build api/Midango.sln
dotnet ef migrations add Bookings --project api/src/Midango.Api --output-dir Data/Migrations
```

In the generated `*_Bookings.cs`:

1. The `free_cancellation_hours` column on `venues`: change its `defaultValue: 0` to `defaultValue: 24`.
2. At the **end of `Up`**, after the `bookings` table and its indexes are created, add:

```csharp
            // The last line of defence against double bookings: two confirmed bookings of one court
            // may never overlap, whatever the application does. Ranges are half-open, so 18:00–19:30
            // and 19:30–21:00 can both exist.
            migrationBuilder.Sql("""
                ALTER TABLE bookings
                ADD CONSTRAINT ex_bookings_no_overlap
                EXCLUDE USING gist (court_id WITH =, tstzrange(starts_at, ends_at, '[)') WITH &&)
                WHERE (status = 'Confirmed');
                """);
```

3. At the **start of `Down`**, add:

```csharp
            migrationBuilder.Sql("ALTER TABLE bookings DROP CONSTRAINT IF EXISTS ex_bookings_no_overlap;");
```

Confirm the migration also contains `AlterDatabase().Annotation("Npgsql:PostgresExtension:btree_gist", …)` (EF creates the extension; the `midango` database owner may create this trusted extension).

- [ ] **Step 5: Apply and inspect**

Start the API once, wait for `/health`, stop it, then:

```powershell
$env:PGPASSWORD = 'midango_dev'
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U midango -h localhost -d midango -c "\dx btree_gist" -c "\d bookings" -c "SELECT free_cancellation_hours FROM venues LIMIT 1;"
Remove-Item Env:PGPASSWORD
```

Expected: `btree_gist` listed; `\d bookings` shows `ex_bookings_no_overlap EXCLUDE USING gist (court_id WITH =, tstzrange(starts_at, ends_at, '[)'::text) WITH &&) WHERE (status = 'Confirmed')` and `ck_bookings_ends_after_start`; venues show `24`.

- [ ] **Step 6: Commit**

```powershell
git add api
git commit -m "feat(api): bookings table with a database-enforced no-overlap constraint"
```

---

### Task 2: Slots, venue clock and availability

**Files:**
- Create: `api/src/Midango.Api/Bookings/Slots.cs`, `api/src/Midango.Api/Bookings/AvailabilityEndpoints.cs`

- [ ] **Step 1: Create `Bookings/Slots.cs`**

```csharp
using Midango.Api.Catalog;
using Midango.Api.Venues;

namespace Midango.Api.Bookings;

/// Converts between UTC instants and the venue's wall clock.
public static class VenueClock
{
    public static TimeZoneInfo Zone(Venue venue) => TimeZoneInfo.FindSystemTimeZoneById(venue.TimeZoneId);

    /// ponytail: takes the zone's offset at that wall-clock time. Correct for Asia/Riyadh (no DST);
    /// ambiguous/skipped times in DST zones need handling before such a venue launches.
    public static DateTimeOffset ToInstant(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified));
    }

    public static DateTime ToLocal(DateTimeOffset instant, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(instant, zone).DateTime;
}

/// Start times a venue sells, from its weekly opening hours. All values are venue-local wall-clock times.
public static class Slots
{
    public const int StepMinutes = 30;
    public const int MaxDaysAhead = 30;

    /// The opening period of one business day; it ends on the next calendar day when it closes after midnight.
    public static (DateTime Opens, DateTime Closes)? Period(DateOnly day, IReadOnlyList<OpeningHours> hours)
    {
        var row = hours.FirstOrDefault(h => h.Day == day.DayOfWeek);
        if (row is null) return null;
        var opens = day.ToDateTime(row.Opens);
        var closes = day.ToDateTime(row.Closes);
        if (row.Closes <= row.Opens) closes = closes.AddDays(1);
        return (opens, closes);
    }

    /// Every StepMinutes from opening while the whole booking fits before closing.
    public static IEnumerable<DateTime> Starts(DateOnly day, IReadOnlyList<OpeningHours> hours, int minutes)
    {
        if (Period(day, hours) is not { } period) yield break;
        for (var start = period.Opens; start.AddMinutes(minutes) <= period.Closes; start = start.AddMinutes(StepMinutes))
            yield return start;
    }

    /// True when the booking is one of the sold start times of its own business day or of the previous one
    /// (a 00:30 start belongs to the evening before when that day closes after midnight).
    public static bool Bookable(DateTime localStart, int minutes, IReadOnlyList<OpeningHours> hours)
    {
        if (localStart.Second != 0 || localStart.Millisecond != 0) return false;
        var day = DateOnly.FromDateTime(localStart);
        return Fits(day) || Fits(day.AddDays(-1));

        bool Fits(DateOnly businessDay) =>
            Period(businessDay, hours) is { } p
            && localStart >= p.Opens
            && localStart.AddMinutes(minutes) <= p.Closes
            && (localStart - p.Opens).TotalMinutes % StepMinutes == 0;
    }
}
```

- [ ] **Step 2: Create `Bookings/AvailabilityEndpoints.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Midango.Api.Catalog;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Bookings;

public static class AvailabilityEndpoints
{
    public sealed record SlotCourtDto(Guid CourtId, decimal Total);

    /// Start carries the venue's offset (e.g. +03:00) so clients can send it straight back when booking.
    public sealed record SlotDto(DateTimeOffset Start, string LocalTime, IReadOnlyList<SlotCourtDto> Courts);

    public sealed record AvailabilityDto(
        Guid VenueId, DateOnly Date, Guid CategoryId, int Minutes, string Currency, bool Open, IReadOnlyList<SlotDto> Slots);

    public static void MapAvailabilityEndpoints(this IEndpointRouteBuilder app)
    {
        // Public: the app's slot chips. Full times stay in the list with no courts, so they can be struck through.
        app.MapGet("/venues/{venueId:guid}/availability", async (
            Guid venueId, DateOnly date, Guid categoryId, int minutes, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleOrDefaultAsync(v => v.Id == venueId && v.Published, ct);
            if (venue is null) return ApiErrors.Fail(404, "Venue not found.");

            var category = await db.SportCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == categoryId && c.Active, ct);
            if (category is null || !category.DurationsMinutes.Contains(minutes))
                return ApiErrors.Fail(400, "Choose one of this sport's booking lengths.");

            var zone = VenueClock.Zone(venue);
            var now = clock.GetUtcNow();
            var today = DateOnly.FromDateTime(VenueClock.ToLocal(now, zone));
            if (date < today || date > today.AddDays(Slots.MaxDaysAhead))
                return ApiErrors.Fail(400, $"Choose a date within the next {Slots.MaxDaysAhead} days.");

            var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venueId).ToListAsync(ct);
            var courts = await db.Courts.AsNoTracking()
                .Where(c => c.VenueId == venueId && c.CategoryId == categoryId && c.Active)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);
            var courtIds = courts.Select(c => c.Id).ToList();
            var bands = (await db.PriceBands.AsNoTracking().Where(b => courtIds.Contains(b.CourtId)).ToListAsync(ct))
                .ToLookup(b => b.CourtId, b => new PriceBandRule(b.Day, b.From, b.To, b.HourlyPrice));

            var starts = Slots.Starts(date, hours, minutes).ToList();
            if (starts.Count == 0)
                return Results.Ok(new AvailabilityDto(venue.Id, date, categoryId, minutes, venue.CurrencyCode, false, []));

            var windowFrom = VenueClock.ToInstant(starts[0], zone).ToUniversalTime();
            var windowTo = VenueClock.ToInstant(starts[^1].AddMinutes(minutes), zone).ToUniversalTime();
            var booked = await db.Bookings.AsNoTracking()
                .Where(b => courtIds.Contains(b.CourtId) && b.Status == BookingStatus.Confirmed
                            && b.StartsAt < windowTo && b.EndsAt > windowFrom)
                .Select(b => new { b.CourtId, b.StartsAt, b.EndsAt })
                .ToListAsync(ct);

            // ponytail: courts × times × bookings in memory; fine for a venue-day (≈ 20 courts × 40 times).
            var slots = new List<SlotDto>();
            foreach (var local in starts)
            {
                var start = VenueClock.ToInstant(local, zone);
                var end = start.AddMinutes(minutes);
                if (start <= now) continue;

                var free = courts
                    .Where(c => !booked.Any(b => b.CourtId == c.Id && b.StartsAt < end && b.EndsAt > start))
                    .Select(c => new SlotCourtDto(c.Id, Pricing.Calculate(
                        c.HourlyPrice, bands[c.Id].ToList(), local.DayOfWeek, TimeOnly.FromDateTime(local), minutes, venue.VatPercent).Total))
                    .ToList();
                slots.Add(new SlotDto(start, local.ToString("HH:mm"), free));
            }

            return Results.Ok(new AvailabilityDto(venue.Id, date, categoryId, minutes, venue.CurrencyCode, true, slots));
        });
    }
}
```

- [ ] **Step 3: Build**

`dotnet build api/Midango.sln` → 0 warnings, 0 errors. (Mapped in Task 3.)

- [ ] **Step 4: Commit**

```powershell
git add api
git commit -m "feat(api): opening-hours slots in the venue's time zone and public availability"
```

---

### Task 3: Booking, listing and cancelling

**Files:**
- Create: `api/src/Midango.Api/Bookings/BookingEndpoints.cs`
- Modify: `api/src/Midango.Api/Program.cs`

- [ ] **Step 1: Create `Bookings/BookingEndpoints.cs`**

```csharp
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
        BookingStatus Status, DateTimeOffset FreeCancellationUntil, bool LateCancellation, DateTimeOffset? CancelledAt);

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

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await db.SaveChangesAsync(ct);
                    break;
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
                {
                    return ApiErrors.Fail(409, "Someone just booked this time. Pick another one.");
                }
                catch (DbUpdateException ex) when (attempt < 3 && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    booking.Reference = BookingReference.New(); // reference collision: draw again
                }
            }

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
                b.Status, Local(b.FreeCancellationUntil), b.LateCancellation, b.CancelledAt is { } c ? Local(c) : null);
        }).ToList();
    }
}
```

- [ ] **Step 2: Map in `Program.cs`**

Add `using Midango.Api.Bookings;` and, after `app.MapPriceEndpoints();`:

```csharp
app.MapAvailabilityEndpoints();
app.MapBookingEndpoints();
```

- [ ] **Step 3: Build and check the model**

```powershell
dotnet build api/Midango.sln
dotnet ef migrations has-pending-model-changes --project api/src/Midango.Api
```

Expected: 0 warnings, 0 errors; no pending changes.

- [ ] **Step 4: Commit**

```powershell
git add api
git commit -m "feat(api): book with cash at the venue, my bookings, cancel with a free-cancellation window"
```

---

### Task 4: Verify against the running API and the database (controller)

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
    # -DateKind String keeps "2026-11-19T20:00:00+03:00" as text instead of a local DateTime.
    if ($text) { try { return $text | ConvertFrom-Json -DateKind String } catch { return $null } }
}
function Check([bool]$Condition, [string]$What) {
    if ($Condition) { Write-Host "PASS $What" } else { $script:failures++; Write-Host "FAIL $What" }
}
function SignInCustomer([string]$Name) {
    $local = '055' + (Get-Random -Minimum 1000000 -Maximum 9999999)
    Call POST /auth/otp/request @{ phone = $local } -Expect 202 | Out-Null
    Start-Sleep -Milliseconds 800
    $code = (Select-String -Path $log -Pattern 'DEV OTP for .*: (\d{6})' | Select-Object -Last 1).Matches[0].Groups[1].Value
    $token = (Call POST /auth/otp/verify @{ phone = $local; code = $code } -Expect 200).accessToken
    if ($Name) { Call PUT /me @{ name = $Name } -Token $token -Expect 200 | Out-Null }
    return $token
}
function Slot($Availability, [string]$Time) { return $Availability.slots | Where-Object localTime -eq $Time }

# Riyadh is UTC+3 with no DST; test two days ahead so every time is in the future.
$day = [DateTime]::UtcNow.AddHours(3).Date.AddDays(2)
$date = $day.ToString('yyyy-MM-dd')
$next = $day.AddDays(1).ToString('yyyy-MM-dd')
function At([string]$Date, [string]$Time) { return "${Date}T${Time}:00+03:00" }

# Setup: a published venue open every day 16:00–02:00 with two padel courts at 150/h.
$op = (Call POST /staff/auth/login @{ email = 'operator@midango.local'; password = 'Operator!2026dev' } -Expect 200).accessToken
$s = Get-Random
$padel = Call POST /admin/categories @{ nameEn = "Padel $s"; nameAr = 'بادل'; iconKey = 'volleyball'; durationsMinutes = @(60, 90); sortOrder = 1; active = $true } -Token $op -Expect 201
$v = Call POST /admin/venues @{ nameEn = "Book $s"; nameAr = 'حجز' } -Token $op -Expect 201
Call POST "/admin/venues/$($v.id)/staff" @{ email = "book-$s@arena.test"; displayName = 'Mgr'; role = 'VenueManager'; temporaryPassword = 'Temp-pass-123' } -Token $op -Expect 201 | Out-Null
$mgr = (Call POST /staff/auth/login @{ email = "book-$s@arena.test"; password = 'Temp-pass-123' } -Expect 200).accessToken
Call PUT /venue @{ nameEn = "Book $s"; nameAr = 'حجز'; addressEn = 'Riyadh'; addressAr = 'الرياض'; latitude = 24.7; longitude = 46.7 } -Token $mgr -Expect 200 | Out-Null
$week = 'Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday' | ForEach-Object { @{ day = $_; opens = '16:00:00'; closes = '02:00:00' } }
Call PUT /venue/hours $week -Token $mgr -Expect 200 | Out-Null
Call POST /venue/courts @{ categoryId = $padel.id; nameEn = 'Court 1'; nameAr = 'ملعب 1'; indoor = $true; hourlyPrice = 150; featureIds = @(); sortOrder = 1; active = $true } -Token $mgr -Expect 201 | Out-Null
$venue = Call POST /venue/courts @{ categoryId = $padel.id; nameEn = 'Court 2'; nameAr = 'ملعب 2'; indoor = $true; hourlyPrice = 150; featureIds = @(); sortOrder = 2; active = $true } -Token $mgr -Expect 201
$c1 = $venue.courts[0].id
$c2 = $venue.courts[1].id
$avail = "/venues/$($v.id)/availability?date=$date&categoryId=$($padel.id)"

# 1. Availability
Call GET "$avail&minutes=90" -Expect 404 | Out-Null
Call POST /venue/publish -Token $mgr -Expect 200 | Out-Null
$a = Call GET "$avail&minutes=90" -Expect 200
Check ($a.open -and $a.slots.Count -eq 18) '16:00 to 00:30 every 30 min = 18 start times for 90 min'
Check ($a.slots[0].localTime -eq '16:00' -and $a.slots[-1].localTime -eq '00:30') 'first 16:00, last 00:30 (ends 02:00)'
Check ((Slot $a '20:00').courts.Count -eq 2 -and (Slot $a '20:00').courts[0].total -eq 225) '20:00: two courts at 225.00'
Check ((Slot $a '20:00').start -like "*+03:00") 'start carries the venue offset'
Call GET "$avail&minutes=45" -Expect 400 | Out-Null
Call GET "/venues/$($v.id)/availability?date=$($day.AddDays(40).ToString('yyyy-MM-dd'))&categoryId=$($padel.id)&minutes=90" -Expect 400 | Out-Null
Call GET "/venues/$($v.id)/availability?date=$($day.AddDays(-5).ToString('yyyy-MM-dd'))&categoryId=$($padel.id)&minutes=90" -Expect 400 | Out-Null

# 2. Booking
$ali = SignInCustomer $null
Call POST /bookings @{ courtId = $c1; start = (At $date '20:00'); minutes = 90 } -Token $ali -Expect 409 | Out-Null
Call PUT /me @{ name = 'Ali' } -Token $ali -Expect 200 | Out-Null
$b1 = Call POST /bookings @{ courtId = $c1; start = (At $date '20:00'); minutes = 90 } -Token $ali -Expect 201
Check ($b1.reference.Length -eq 6 -and $b1.total -eq 225 -and $b1.paymentMethod -eq 'CashAtVenue' -and $b1.status -eq 'Confirmed') 'booked: 6-char reference, 225.00, cash at venue'
Check ($b1.startsAt -like "*T20:00:00+03:00") 'booking time shown in venue time'
$a = Call GET "$avail&minutes=90" -Expect 200
Check ((Slot $a '20:00').courts.Count -eq 1) '20:00 now has one free court'
Check ((Slot $a '19:00').courts.Count -eq 1) '19:00 (overlaps 20:00) has one free court'
Check ((Slot $a '18:30').courts.Count -eq 2) '18:30 (ends exactly at 20:00) still has two'
Check ((Slot $a '21:30').courts.Count -eq 2) '21:30 (starts exactly at the end) still has two'

Call POST /bookings @{ courtId = $c1; start = (At $date '20:30'); minutes = 90 } -Token $ali -Expect 409 | Out-Null
Call POST /bookings @{ courtId = $c1; start = (At $date '21:30'); minutes = 60 } -Token $ali -Expect 201 | Out-Null
Call POST /bookings @{ courtId = $c1; start = (At $date '20:15'); minutes = 60 } -Token $ali -Expect 400 | Out-Null
Call POST /bookings @{ courtId = $c1; start = (At $next '01:30'); minutes = 90 } -Token $ali -Expect 400 | Out-Null
$late = Call POST /bookings @{ courtId = $c2; start = (At $next '00:30'); minutes = 90 } -Token $ali -Expect 201
Check ($late.startsAt -like "${next}T00:30:00+03:00") 'past-midnight booking 00:30–02:00 belongs to the evening before'
Call POST /bookings @{ courtId = $c1; start = (At $date '20:00'); minutes = 45 } -Token $ali -Expect 400 | Out-Null
Call POST /bookings @{ courtId = $c1; start = '2020-01-01T20:00:00+03:00'; minutes = 90 } -Token $ali -Expect 400 | Out-Null
Call POST /bookings @{ courtId = $c1; start = (At $date '20:00'); minutes = 90 } -Expect 401 | Out-Null
Call POST /bookings @{ courtId = $c1; start = (At $date '20:00'); minutes = 90 } -Token $mgr -Expect 403 | Out-Null

# 3. The race: five simultaneous requests for the same court and time
$raceStart = At $date '18:00'
$codes = 1..5 | ForEach-Object -Parallel {
    $body = @{ courtId = $using:c2; start = $using:raceStart; minutes = 90 } | ConvertTo-Json -Compress
    [int](Invoke-WebRequest -Uri "$($using:api)/bookings" -Method POST -Body $body -ContentType 'application/json' `
        -Headers @{ Authorization = "Bearer $($using:ali)" } -SkipHttpErrorCheck).StatusCode
} -ThrottleLimit 5
Check ((@($codes | Where-Object { $_ -eq 201 }).Count -eq 1) -and (@($codes | Where-Object { $_ -eq 409 }).Count -eq 4)) "race: exactly one 201 and four 409 (got $($codes -join ','))"

# 4. My bookings and cancelling
$mine = Call GET /me/bookings -Token $ali -Expect 200
Check ($mine.upcoming.Count -eq 4 -and $mine.history.Count -eq 0) 'four upcoming bookings, nothing in history'
Check ($mine.upcoming[0].startsAt -like "*T18:00:00+03:00") 'upcoming sorted by start'
$sara = SignInCustomer 'Sara'
Call POST "/bookings/$($b1.id)/cancel" -Token $sara -Expect 404 | Out-Null
$cancelled = Call POST "/bookings/$($b1.id)/cancel" -Token $ali -Expect 200
Check ($cancelled.status -eq 'Cancelled' -and $cancelled.lateCancellation -eq $false) 'cancelled two days ahead: free, not late'
Call POST "/bookings/$($b1.id)/cancel" -Token $ali -Expect 200 | Out-Null
$a = Call GET "$avail&minutes=90" -Expect 200
Check ((Slot $a '20:00').courts.Count -eq 2) 'cancelled time is free again'
$again = Call POST /bookings @{ courtId = $c1; start = (At $date '20:00'); minutes = 90 } -Token $sara -Expect 201
$mine = Call GET /me/bookings -Token $ali -Expect 200
Check ($mine.upcoming.Count -eq 3 -and $mine.history.Count -eq 1 -and $mine.history[0].status -eq 'Cancelled') 'cancelled booking moved to history'

Write-Host "`nFailures: $script:failures"
```

Expected: every line `PASS`, `Failures: 0`.

- [ ] **Step 3: Prove the constraint holds without the application**

```powershell
$env:PGPASSWORD = 'midango_dev'
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U midango -h localhost -d midango -c @"
INSERT INTO bookings (id, reference, venue_id, court_id, customer_id, starts_at, ends_at, minutes, total, net, vat, currency, status, created_at, free_cancellation_until, late_cancellation)
SELECT gen_random_uuid(), 'SQLTST', venue_id, court_id, customer_id, starts_at + interval '30 minutes', ends_at + interval '30 minutes', minutes, total, net, vat, currency, 'Confirmed', now(), free_cancellation_until, false
FROM bookings WHERE status = 'Confirmed' LIMIT 1;
"@
Remove-Item Env:PGPASSWORD
```

Expected: `ERROR: conflicting key value violates exclusion constraint "ex_bookings_no_overlap"`.

- [ ] **Step 4:** Re-run the M2, M3a and M4 scripts (`Failures: 0`), stop the API, tick M5 in the roadmap, and commit.

---

## What M5 deliberately leaves out

- **Staff-created bookings, walk-ins, moving bookings, the schedule grid:** M6. Customer phone numbers stay hidden from staff (Q2).
- **Check-in, cash collected, no-show:** M7 (Desk).
- **No-show limits and reminders:** M9. `lateCancellation` is recorded now, ready for them.
- **Recurring and group bookings:** later.
- **A venue setting for the free-cancellation hours:** stored (default 24) and editable when the M6 venue settings screen arrives.
- **A slot hold:** not needed while payment is cash at the venue. Revisit when online payment arrives.
