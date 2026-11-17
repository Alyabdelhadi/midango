# M3a Catalog API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The API can describe what a venue offers: platform-managed sport categories (with court features and booking durations), venue address and map location, weekly opening hours, and courts. A venue can be published only when it is complete.

**Architecture:** New `Catalog/` feature folder alongside `Identity/`. Same patterns as M2: EF Core entities + one new migration, minimal-API endpoint groups guarded by the M2 policies, `ApiErrors.Fail` for errors with sentences a person can act on. Every venue write returns the full venue (details, hours, courts, and what is still missing to publish), so clients never have to stitch several calls together.

**Tech Stack:** .NET 10 · EF Core 10 / Npgsql (PostgreSQL `integer[]` for durations) · existing M2 auth policies

**Project rules:** No automated tests; verification is a scripted run against the live API (Task 4). PowerShell for commands. Keep every package on **10.x** (NuGet now offers 11.x; never let `dotnet add` pick it).

**Decisions this plan locks in:**

| Topic | Decision | Why |
|---|---|---|
| Sport categories | Created and edited only by the platform operator (Q5). Both names required. `iconKey` is a Lucide icon name in kebab-case; clients fall back to a default icon for names they don't know | No hardcoded sports; icon set stays one family (D9) without the API keeping a list of icons |
| Durations | Per category, 1–4 values, 30–240 min in steps of 30 (e.g. padel 60/90/120) | Drives duration chips in the app and the Business booking popover |
| Court features | Per category (padel: Panoramic; football: 5-a-side). Indoor/outdoor is a plain court field because every sport has it | Features become the app's filters for that sport |
| Opening hours | One period per day; `closes ≤ opens` means after midnight (16:00–02:00). A day with no row is closed. Week starts Sunday | GCC venues commonly close after midnight |
| Time zone | Per venue, IANA id, default `Asia/Riyadh` | Slot calculation (M5) needs it |
| Publish | Needs address (EN+AR), map location, at least one opening day, at least one open court. A published venue refuses any edit that would break that (409) | The app never shows a venue nobody can book |
| Who edits | Venue manager edits own venue; desk staff can read it; other venues' courts return 404 | Venue scoping from M2 |

## Endpoints delivered

| Method & path | Who | Result |
|---|---|---|
| `GET /categories` | anyone | Active categories in order, with features and durations |
| `GET /admin/categories` · `POST /admin/categories` · `PUT /admin/categories/{id}` | platform operator | All categories · 201 · 200 (features matched by id: kept, renamed, added, removed) |
| `GET /venue` | venue manager, desk staff | Own venue: details, hours, courts, `missingForPublish` |
| `PUT /venue` | venue manager | Names, address, location, time zone |
| `PUT /venue/hours` | venue manager | Replaces the week |
| `POST /venue/courts` · `PUT /venue/courts/{id}` | venue manager | Add / edit a court |
| `POST /venue/publish` · `POST /venue/unpublish` | venue manager | 200 · 409 with what's missing |

## File structure

```
api/src/Midango.Api/
├── Program.cs                 modified: map the two new endpoint groups
├── Data/MidangoDb.cs          modified: new DbSets + configuration
├── Data/Migrations/           new migration "Catalog"
├── Venues/Venue.cs            modified: address, location, time zone, published
└── Catalog/
    ├── Entities.cs            SportCategory, CourtFeature, Court, OpeningHours
    ├── Input.cs               shared bilingual-text check
    ├── CategoryEndpoints.cs   /categories, /admin/categories
    └── VenueEndpoints.cs      /venue, /venue/hours, /venue/courts, publish
```

---

### Task 1: Catalog data model and migration

**Files:**
- Create: `api/src/Midango.Api/Catalog/Entities.cs`
- Modify: `api/src/Midango.Api/Venues/Venue.cs` (replace), `api/src/Midango.Api/Data/MidangoDb.cs` (replace)
- Create: `api/src/Midango.Api/Data/Migrations/*_Catalog.cs` (generated)

- [ ] **Step 1: Replace `Venues/Venue.cs`**

```csharp
namespace Midango.Api.Venues;

public sealed class Venue
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string NameEn { get; set; }
    public required string NameAr { get; set; }
    public string? AddressEn { get; set; }
    public string? AddressAr { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// IANA id. Slot times (M5) are calculated in the venue's own time zone.
    public string TimeZoneId { get; set; } = "Asia/Riyadh";

    /// Visible in the app. Only allowed when nothing is missing (see VenueEndpoints.MissingForPublish).
    public bool Published { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 2: Create `Catalog/Entities.cs`**

```csharp
namespace Midango.Api.Catalog;

/// A sport the platform supports. Created only by the platform operator.
public sealed class SportCategory
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string NameEn { get; set; }
    public required string NameAr { get; set; }

    /// Lucide icon name in kebab-case, e.g. "volleyball". Clients fall back to a default icon if unknown.
    public required string IconKey { get; set; }

    /// Bookable lengths in minutes, ascending, e.g. [60, 90, 120].
    public int[] DurationsMinutes { get; set; } = [];
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
    public List<CourtFeature> Features { get; set; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// A sport-specific court attribute that customers can filter by, e.g. "Panoramic".
public sealed class CourtFeature
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CategoryId { get; init; }
    public required string NameEn { get; set; }
    public required string NameAr { get; set; }
    public int SortOrder { get; set; }
}

public sealed class Court
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid VenueId { get; init; }
    public Guid CategoryId { get; set; }
    public required string NameEn { get; set; }
    public required string NameAr { get; set; }
    public bool Indoor { get; set; }

    /// Always features of this court's category.
    public List<CourtFeature> Features { get; set; } = [];
    public int SortOrder { get; set; }

    /// False = closed (not bookable); shown as such in Business.
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// One opening period for one day of the week. A day with no row is closed.
public sealed class OpeningHours
{
    public Guid VenueId { get; init; }
    public DayOfWeek Day { get; init; }
    public TimeOnly Opens { get; set; }

    /// Closes <= Opens means the venue closes after midnight (e.g. 16:00–02:00).
    public TimeOnly Closes { get; set; }
}
```

- [ ] **Step 3: Replace `Data/MidangoDb.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Midango.Api.Catalog;
using Midango.Api.Identity;
using Midango.Api.Venues;

namespace Midango.Api.Data;

public sealed class MidangoDb(DbContextOptions<MidangoDb> options) : DbContext(options)
{
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<StaffMember> Staff => Set<StaffMember>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SportCategory> SportCategories => Set<SportCategory>();
    public DbSet<CourtFeature> CourtFeatures => Set<CourtFeature>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<OpeningHours> OpeningHours => Set<OpeningHours>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Venue>(e =>
        {
            e.Property(v => v.NameEn).HasMaxLength(120);
            e.Property(v => v.NameAr).HasMaxLength(120);
            e.Property(v => v.AddressEn).HasMaxLength(200);
            e.Property(v => v.AddressAr).HasMaxLength(200);
            e.Property(v => v.TimeZoneId).HasMaxLength(64);
        });

        b.Entity<Customer>(e =>
        {
            e.Property(c => c.PhoneE164).HasMaxLength(16);
            e.HasIndex(c => c.PhoneE164).IsUnique();
            e.Property(c => c.Name).HasMaxLength(60);
        });

        b.Entity<StaffMember>(e =>
        {
            e.ToTable("staff", t => t.HasCheckConstraint(
                "ck_staff_venue_matches_role",
                "(role = 'PlatformOperator') = (venue_id IS NULL)"));
            e.Property(s => s.Email).HasMaxLength(254);
            e.HasIndex(s => s.Email).IsUnique();
            e.Property(s => s.DisplayName).HasMaxLength(80);
            e.Property(s => s.Role).HasConversion<string>().HasMaxLength(32);
            e.HasOne<Venue>().WithMany().HasForeignKey(s => s.VenueId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OtpChallenge>(e =>
        {
            e.Property(c => c.PhoneE164).HasMaxLength(16);
            e.Property(c => c.CodeHash).HasMaxLength(64);
            e.HasIndex(c => new { c.PhoneE164, c.CreatedAt });
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(t => t.TokenHash).HasMaxLength(64);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.Property(t => t.SubjectKind).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(t => new { t.SubjectKind, t.SubjectId });
        });

        b.Entity<SportCategory>(e =>
        {
            e.ToTable("sport_categories", t => t.HasCheckConstraint(
                "ck_sport_categories_durations", "cardinality(durations_minutes) BETWEEN 1 AND 4"));
            e.Property(c => c.NameEn).HasMaxLength(60);
            e.Property(c => c.NameAr).HasMaxLength(60);
            e.Property(c => c.IconKey).HasMaxLength(40);
            e.HasMany(c => c.Features).WithOne().HasForeignKey(f => f.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CourtFeature>(e =>
        {
            e.Property(f => f.NameEn).HasMaxLength(60);
            e.Property(f => f.NameAr).HasMaxLength(60);
        });

        b.Entity<Court>(e =>
        {
            e.Property(c => c.NameEn).HasMaxLength(60);
            e.Property(c => c.NameAr).HasMaxLength(60);
            e.HasOne<Venue>().WithMany().HasForeignKey(c => c.VenueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SportCategory>().WithMany().HasForeignKey(c => c.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(c => c.Features).WithMany().UsingEntity(j => j.ToTable("court_feature_links"));
            e.HasIndex(c => new { c.VenueId, c.SortOrder });
        });

        b.Entity<OpeningHours>(e =>
        {
            e.ToTable("opening_hours", t => t.HasCheckConstraint("ck_opening_hours_not_empty", "opens <> closes"));
            e.HasKey(h => new { h.VenueId, h.Day });
            e.HasOne<Venue>().WithMany().HasForeignKey(h => h.VenueId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

- [ ] **Step 4: Generate and inspect the migration**

```powershell
cd C:\Users\Lenovo\Desktop\Ali\midango
dotnet build api/Midango.sln
dotnet ef migrations add Catalog --project api/src/Midango.Api --output-dir Data/Migrations
```

Expected: `Done.` Open `*_Catalog.cs` and confirm:
- new columns on `venues`: `address_en`, `address_ar`, `latitude`, `longitude`, `time_zone_id` (default `''` is acceptable only if the migration sets a default. If EF adds `time_zone_id` as non-null with default `""`, change that `defaultValue` to `"Asia/Riyadh"` in the generated migration so existing venues get a real zone), `published`;
- new tables `sport_categories` (with `durations_minutes integer[]` and check `ck_sport_categories_durations`), `court_features`, `courts`, `court_feature_links`, `opening_hours` (primary key `venue_id, day`, check `ck_opening_hours_not_empty`).

- [ ] **Step 5: Apply by starting the API once, then list tables**

```powershell
$p = Start-Process dotnet -ArgumentList 'run','--project','api/src/Midango.Api','--urls','http://localhost:5300' -PassThru -WindowStyle Hidden
for ($i = 0; $i -lt 30; $i++) { Start-Sleep 2; $h = curl.exe -s http://localhost:5300/health; if ($h) { break } }
$h
Get-NetTCPConnection -LocalPort 5300 -State Listen -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
$env:PGPASSWORD = 'midango_dev'
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U midango -h localhost -d midango -c "\dt" -c "SELECT name_en, time_zone_id, published FROM venues LIMIT 3;"
Remove-Item Env:PGPASSWORD
```

Expected: health ok. `\dt` adds `court_feature_links`, `court_features`, `courts`, `opening_hours`, `sport_categories`. Existing venues show `Asia/Riyadh | f`.

- [ ] **Step 6: Commit**

```powershell
git add api
git commit -m "feat(api): catalog tables - sport categories, court features, courts, opening hours"
```

---

### Task 2: Sport category endpoints

**Files:**
- Create: `api/src/Midango.Api/Catalog/Input.cs`, `api/src/Midango.Api/Catalog/CategoryEndpoints.cs`

- [ ] **Step 1: Create `Catalog/Input.cs`**

```csharp
namespace Midango.Api.Catalog;

public static class Input
{
    /// Both languages present and within the limit. The app is bilingual, so a missing
    /// translation would show as a blank label.
    public static bool Bilingual(string? en, string? ar, int maxLength) =>
        !string.IsNullOrWhiteSpace(en) && !string.IsNullOrWhiteSpace(ar)
        && en.Trim().Length <= maxLength && ar.Trim().Length <= maxLength;
}
```

- [ ] **Step 2: Create `Catalog/CategoryEndpoints.cs`**

```csharp
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Catalog;

public static partial class CategoryEndpoints
{
    public sealed record FeatureDto(Guid Id, string NameEn, string NameAr);

    public sealed record CategoryDto(
        Guid Id, string NameEn, string NameAr, string IconKey, int[] DurationsMinutes,
        int SortOrder, bool Active, IReadOnlyList<FeatureDto> Features)
    {
        public static CategoryDto From(SportCategory c) => new(
            c.Id, c.NameEn, c.NameAr, c.IconKey, c.DurationsMinutes, c.SortOrder, c.Active,
            c.Features.OrderBy(f => f.SortOrder).Select(f => new FeatureDto(f.Id, f.NameEn, f.NameAr)).ToList());
    }

    /// Features are listed in display order. An item with an id renames that feature;
    /// an item without one adds a feature; features left out are removed.
    public sealed record FeatureInput(Guid? Id, string? NameEn, string? NameAr);

    public sealed record CategoryInput(
        string? NameEn, string? NameAr, string? IconKey, int[]? DurationsMinutes,
        int SortOrder, bool Active, IReadOnlyList<FeatureInput>? Features);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IconKeyPattern();

    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        // Public: the app builds its sport chips and filters from this.
        app.MapGet("/categories", async (MidangoDb db, CancellationToken ct) =>
        {
            var categories = await db.SportCategories.Include(c => c.Features)
                .Where(c => c.Active).OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);
            return Results.Ok(categories.Select(CategoryDto.From));
        });

        var admin = app.MapGroup("/admin/categories").RequireAuthorization(Policies.PlatformOperator);

        admin.MapGet("", async (MidangoDb db, CancellationToken ct) =>
        {
            var categories = await db.SportCategories.Include(c => c.Features)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);
            return Results.Ok(categories.Select(CategoryDto.From));
        });

        admin.MapPost("", async (CategoryInput input, MidangoDb db, CancellationToken ct) =>
        {
            if (Validate(input) is { } error) return ApiErrors.Fail(400, error);
            if (input.Features?.Any(f => f.Id is not null) == true)
                return ApiErrors.Fail(400, "A new category can't refer to existing features.");

            var category = new SportCategory { NameEn = "", NameAr = "", IconKey = "" };
            Apply(category, input);
            db.SportCategories.Add(category);
            await db.SaveChangesAsync(ct);
            return Results.Json(CategoryDto.From(category), statusCode: 201);
        });

        admin.MapPut("/{id:guid}", async (Guid id, CategoryInput input, MidangoDb db, CancellationToken ct) =>
        {
            if (Validate(input) is { } error) return ApiErrors.Fail(400, error);
            var category = await db.SportCategories.Include(c => c.Features).SingleOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return ApiErrors.Fail(404, "Category not found.");

            var known = category.Features.Select(f => f.Id).ToHashSet();
            if (input.Features?.Any(f => f.Id is { } fid && !known.Contains(fid)) == true)
                return ApiErrors.Fail(400, "One of the features doesn't belong to this category.");

            Apply(category, input);
            await db.SaveChangesAsync(ct);
            return Results.Ok(CategoryDto.From(category));
        });
    }

    private static string? Validate(CategoryInput input)
    {
        if (!Input.Bilingual(input.NameEn, input.NameAr, 60))
            return "Enter the category name in English and Arabic, up to 60 characters each.";
        if (input.IconKey is null || input.IconKey.Length > 40 || !IconKeyPattern().IsMatch(input.IconKey))
            return "Choose an icon.";

        var durations = input.DurationsMinutes ?? [];
        if (durations.Length is < 1 or > 4
            || durations.Any(m => m is < 30 or > 240 || m % 30 != 0)
            || durations.Distinct().Count() != durations.Length)
            return "Choose 1 to 4 booking lengths between 30 and 240 minutes, in steps of 30.";

        if (input.Features?.Any(f => !Input.Bilingual(f.NameEn, f.NameAr, 60)) == true)
            return "Enter every court feature in English and Arabic, up to 60 characters each.";
        return null;
    }

    private static void Apply(SportCategory category, CategoryInput input)
    {
        category.NameEn = input.NameEn!.Trim();
        category.NameAr = input.NameAr!.Trim();
        category.IconKey = input.IconKey!;
        category.DurationsMinutes = [.. input.DurationsMinutes!.Order()];
        category.SortOrder = input.SortOrder;
        category.Active = input.Active;

        var wanted = input.Features ?? [];
        category.Features.RemoveAll(f => !wanted.Any(w => w.Id == f.Id));
        for (var position = 0; position < wanted.Count; position++)
        {
            var item = wanted[position];
            var feature = category.Features.FirstOrDefault(f => f.Id == item.Id);
            if (feature is null)
            {
                feature = new CourtFeature { CategoryId = category.Id, NameEn = "", NameAr = "" };
                category.Features.Add(feature);
            }
            feature.NameEn = item.NameEn!.Trim();
            feature.NameAr = item.NameAr!.Trim();
            feature.SortOrder = position;
        }
    }
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build api/Midango.sln
```

Expected: 0 warnings, 0 errors. (Endpoints are mapped in Task 3.)

- [ ] **Step 4: Commit**

```powershell
git add api
git commit -m "feat(api): sport categories with features and durations, public and operator endpoints"
```

---

### Task 3: Venue details, hours, courts and publishing

**Files:**
- Create: `api/src/Midango.Api/Catalog/VenueEndpoints.cs`
- Modify: `api/src/Midango.Api/Program.cs`

- [ ] **Step 1: Create `Catalog/VenueEndpoints.cs`**

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;
using Midango.Api.Venues;

namespace Midango.Api.Catalog;

public static class VenueEndpoints
{
    public sealed record HoursDto(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);

    public sealed record CourtDto(
        Guid Id, Guid CategoryId, string NameEn, string NameAr, bool Indoor,
        IReadOnlyList<Guid> FeatureIds, int SortOrder, bool Active)
    {
        public static CourtDto From(Court c) => new(
            c.Id, c.CategoryId, c.NameEn, c.NameAr, c.Indoor,
            c.Features.Select(f => f.Id).ToList(), c.SortOrder, c.Active);
    }

    /// missingForPublish holds keys the client translates: "address", "location", "hours", "courts".
    public sealed record VenueDto(
        Guid Id, string NameEn, string NameAr, string? AddressEn, string? AddressAr,
        double? Latitude, double? Longitude, string TimeZoneId, bool Published,
        IReadOnlyList<HoursDto> Hours, IReadOnlyList<CourtDto> Courts, IReadOnlyList<string> MissingForPublish);

    public sealed record VenueInput(
        string? NameEn, string? NameAr, string? AddressEn, string? AddressAr,
        double? Latitude, double? Longitude, string? TimeZoneId);

    public sealed record CourtInput(
        Guid CategoryId, string? NameEn, string? NameAr, bool Indoor, Guid[]? FeatureIds, int SortOrder, bool Active);

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
            : Results.Problem(statusCode: 409, title: "A published venue can't lose this. Unpublish it first.",
                extensions: new Dictionary<string, object?> { ["missing"] = missing });
    }

    private static async Task<VenueDto?> LoadAsync(MidangoDb db, Guid venueId, CancellationToken ct)
    {
        var venue = await db.Venues.AsNoTracking().SingleOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null) return null;
        var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == venueId).OrderBy(h => h.Day).ToListAsync(ct);
        var courts = await db.Courts.AsNoTracking().Include(c => c.Features)
            .Where(c => c.VenueId == venueId).OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);

        return new VenueDto(
            venue.Id, venue.NameEn, venue.NameAr, venue.AddressEn, venue.AddressAr,
            venue.Latitude, venue.Longitude, venue.TimeZoneId, venue.Published,
            hours.Select(h => new HoursDto(h.Day, h.Opens, h.Closes)).ToList(),
            courts.Select(CourtDto.From).ToList(),
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
        return null;
    }

    /// Validates and copies the input onto the court. Returns an error sentence, or null when applied.
    private static async Task<string?> ApplyCourtAsync(MidangoDb db, Court court, CourtInput input, CancellationToken ct)
    {
        if (!Input.Bilingual(input.NameEn, input.NameAr, 60))
            return "Enter the court name in English and Arabic, up to 60 characters each.";

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
        court.SortOrder = input.SortOrder;
        court.Active = input.Active;
        court.Features.Clear();
        court.Features.AddRange(features);
        return null;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

- [ ] **Step 2: Map the endpoints in `Program.cs`**

Add `using Midango.Api.Catalog;` to the usings at the top, and directly after `app.MapAdminEndpoints();` add:

```csharp
app.MapCategoryEndpoints();
app.MapVenueEndpoints();
```

- [ ] **Step 3: Build and check the model**

```powershell
dotnet build api/Midango.sln
dotnet ef migrations has-pending-model-changes --project api/src/Midango.Api
```

Expected: 0 warnings, 0 errors; "No changes have been made to the model since the last migration."

- [ ] **Step 4: Commit**

```powershell
git add api
git commit -m "feat(api): venue details, opening hours, courts and publish checklist"
```

---

### Task 4: Verify against the running API and the database

- [ ] **Step 1: Start the API** (same as M2: `Start-Process dotnet … -RedirectStandardOutput "$env:TEMP\midango-api.log"`, wait for `/health`).

- [ ] **Step 2: Run this script (save outside the repo, run with `pwsh -NoProfile -File`)**

```powershell
$api = 'http://localhost:5300'
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
    $short = if ($text.Length -gt 160) { $text.Substring(0, 160) + '...' } else { $text }
    Write-Host "$mark $Method $Path -> $([int]$r.StatusCode) (expected $Expect) $short"
    if ($text) { try { return $text | ConvertFrom-Json } catch { return $null } }
}
function Check([bool]$Condition, [string]$What) {
    if ($Condition) { Write-Host "PASS $What" } else { $script:failures++; Write-Host "FAIL $What" }
}

$op = (Call POST /staff/auth/login @{ email = 'operator@midango.local'; password = 'Operator!2026dev' } -Expect 200).accessToken
$s = Get-Random

# 1. Sport categories
Call POST /admin/categories @{ nameEn = 'Padel'; nameAr = ''; iconKey = 'volleyball'; durationsMinutes = @(60); sortOrder = 1; active = $true } -Token $op -Expect 400 | Out-Null
Call POST /admin/categories @{ nameEn = 'Padel'; nameAr = 'بادل'; iconKey = 'Volley Ball'; durationsMinutes = @(60); sortOrder = 1; active = $true } -Token $op -Expect 400 | Out-Null
Call POST /admin/categories @{ nameEn = 'Padel'; nameAr = 'بادل'; iconKey = 'volleyball'; durationsMinutes = @(45); sortOrder = 1; active = $true } -Token $op -Expect 400 | Out-Null
$padel = Call POST /admin/categories @{ nameEn = "Padel $s"; nameAr = 'بادل'; iconKey = 'volleyball'; durationsMinutes = @(120, 60, 90); sortOrder = 1; active = $true
    features = @(@{ nameEn = 'Panoramic'; nameAr = 'بانورامي' }, @{ nameEn = 'Covered'; nameAr = 'مغطى' }) } -Token $op -Expect 201
Check (($padel.durationsMinutes -join ',') -eq '60,90,120') 'durations stored ascending'
$football = Call POST /admin/categories @{ nameEn = "Football $s"; nameAr = 'كرة قدم'; iconKey = 'goal'; durationsMinutes = @(60); sortOrder = 2; active = $true
    features = @(@{ nameEn = '5-a-side'; nameAr = 'خماسي' }) } -Token $op -Expect 201
Call POST /admin/categories @{ nameEn = 'X'; nameAr = 'س'; iconKey = 'x'; durationsMinutes = @(60); sortOrder = 9; active = $true } -Expect 401 | Out-Null

$panoramic = $padel.features[0]
$covered = $padel.features[1]
# rename Panoramic, drop Covered, add Lit
$padel = Call PUT "/admin/categories/$($padel.id)" @{ nameEn = "Padel $s"; nameAr = 'بادل'; iconKey = 'volleyball'; durationsMinutes = @(60, 90, 120); sortOrder = 1; active = $true
    features = @(@{ id = $panoramic.id; nameEn = 'Panoramic glass'; nameAr = 'زجاج بانورامي' }, @{ nameEn = 'Floodlit'; nameAr = 'مضاء' }) } -Token $op -Expect 200
Check ($padel.features.Count -eq 2 -and $padel.features[0].id -eq $panoramic.id -and $padel.features[0].nameEn -eq 'Panoramic glass') 'feature kept by id and renamed; Covered removed; Floodlit added'
Call PUT "/admin/categories/$($padel.id)" @{ nameEn = "Padel $s"; nameAr = 'بادل'; iconKey = 'volleyball'; durationsMinutes = @(60); sortOrder = 1; active = $true
    features = @(@{ id = $football.features[0].id; nameEn = 'Stolen'; nameAr = 'س' }) } -Token $op -Expect 400 | Out-Null

$public = Call GET /categories -Expect 200
Check (@($public | Where-Object id -eq $football.id).Count -eq 1) 'football visible publicly'
Call PUT "/admin/categories/$($football.id)" @{ nameEn = "Football $s"; nameAr = 'كرة قدم'; iconKey = 'goal'; durationsMinutes = @(60); sortOrder = 2; active = $false
    features = @(@{ id = $football.features[0].id; nameEn = '5-a-side'; nameAr = 'خماسي' }) } -Token $op -Expect 200 | Out-Null
$public = Call GET /categories -Expect 200
Check (@($public | Where-Object id -eq $football.id).Count -eq 0) 'inactive football hidden publicly'
$all = Call GET /admin/categories -Token $op -Expect 200
Check (@($all | Where-Object id -eq $football.id).Count -eq 1) 'inactive football still listed for the operator'

# 2. Two venues, each with a manager; one desk staff
function NewVenueManager([string]$Tag) {
    $v = Call POST /admin/venues @{ nameEn = "Arena $Tag $s"; nameAr = "أرينا $Tag" } -Token $op -Expect 201
    $email = "mgr-$Tag-$s@arena.test"
    Call POST "/admin/venues/$($v.id)/staff" @{ email = $email; displayName = "Manager $Tag"; role = 'VenueManager'; temporaryPassword = 'Temp-pass-123' } -Token $op -Expect 201 | Out-Null
    return (Call POST /staff/auth/login @{ email = $email; password = 'Temp-pass-123' } -Expect 200).accessToken
}
$mgr = NewVenueManager 'a'
$other = NewVenueManager 'b'
Call POST /venue/staff @{ email = "desk-$s@arena.test"; displayName = 'Lina'; temporaryPassword = 'Desk-pass-123' } -Token $mgr -Expect 201 | Out-Null
$desk = (Call POST /staff/auth/login @{ email = "desk-$s@arena.test"; password = 'Desk-pass-123' } -Expect 200).accessToken

# 3. Venue setup and the publish checklist
$venue = Call GET /venue -Token $mgr -Expect 200
Check (($venue.missingForPublish -join ',') -eq 'address,location,hours,courts') 'new venue misses everything'
Call POST /venue/publish -Token $mgr -Expect 409 | Out-Null
Call PUT /venue @{ nameEn = "Arena a $s"; nameAr = 'أرينا'; addressEn = 'King Fahd Rd, Riyadh'; addressAr = '' ; latitude = 24.7; longitude = 46.7 } -Token $mgr -Expect 400 | Out-Null
Call PUT /venue @{ nameEn = "Arena a $s"; nameAr = 'أرينا'; addressEn = 'King Fahd Rd, Riyadh'; addressAr = 'طريق الملك فهد، الرياض'; latitude = 24.7136; longitude = 46.6753; timeZoneId = 'Mars/Olympus' } -Token $mgr -Expect 400 | Out-Null
Call PUT /venue @{ nameEn = "Arena a $s"; nameAr = 'أرينا'; addressEn = 'King Fahd Rd, Riyadh'; addressAr = 'طريق الملك فهد، الرياض'; latitude = 24.7136; longitude = 46.6753 } -Token $mgr -Expect 200 | Out-Null

Call PUT /venue/hours @(@{ day = 'Sunday'; opens = '16:00:00'; closes = '16:00:00' }) -Token $mgr -Expect 400 | Out-Null
Call PUT /venue/hours @(@{ day = 'Sunday'; opens = '16:00:00'; closes = '02:00:00' }, @{ day = 'Sunday'; opens = '10:00:00'; closes = '12:00:00' }) -Token $mgr -Expect 400 | Out-Null
$week = 'Sunday','Monday','Tuesday','Wednesday','Thursday' | ForEach-Object { @{ day = $_; opens = '16:00:00'; closes = '02:00:00' } }
$week += @{ day = 'Friday'; opens = '14:00:00'; closes = '02:00:00' }
$venue = Call PUT /venue/hours $week -Token $mgr -Expect 200
Check ($venue.hours.Count -eq 6) 'six opening days stored (Saturday closed)'

Call POST /venue/courts @{ categoryId = $padel.id; nameEn = 'Court 1'; nameAr = 'ملعب 1'; indoor = $true; featureIds = @($football.features[0].id); sortOrder = 1; active = $true } -Token $mgr -Expect 400 | Out-Null
Call POST /venue/courts @{ categoryId = $football.id; nameEn = 'Pitch'; nameAr = 'ملعب'; indoor = $false; featureIds = @(); sortOrder = 1; active = $true } -Token $mgr -Expect 400 | Out-Null
$venue = Call POST /venue/courts @{ categoryId = $padel.id; nameEn = 'Court 1'; nameAr = 'ملعب 1'; indoor = $true; featureIds = @($panoramic.id); sortOrder = 1; active = $true } -Token $mgr -Expect 201
$venue = Call POST /venue/courts @{ categoryId = $padel.id; nameEn = 'Court 2'; nameAr = 'ملعب 2'; indoor = $false; featureIds = @(); sortOrder = 2; active = $true } -Token $mgr -Expect 201
Check ($venue.missingForPublish.Count -eq 0) 'nothing missing after setup'
$court1 = $venue.courts[0]
$court2 = $venue.courts[1]
Check (($court1.featureIds -join ',') -eq $panoramic.id) 'court 1 has the Panoramic feature'

$venue = Call POST /venue/publish -Token $mgr -Expect 200
Check ($venue.published -eq $true) 'venue published'

# 4. A published venue stays complete
Call PUT /venue/hours @() -Token $mgr -Expect 409 | Out-Null
Call PUT "/venue/courts/$($court1.id)" @{ categoryId = $padel.id; nameEn = 'Court 1'; nameAr = 'ملعب 1'; indoor = $true; featureIds = @($panoramic.id); sortOrder = 1; active = $false } -Token $mgr -Expect 200 | Out-Null
Call PUT "/venue/courts/$($court2.id)" @{ categoryId = $padel.id; nameEn = 'Court 2'; nameAr = 'ملعب 2'; indoor = $false; featureIds = @(); sortOrder = 2; active = $false } -Token $mgr -Expect 409 | Out-Null
Call PUT /venue @{ nameEn = "Arena a $s"; nameAr = 'أرينا'; latitude = 24.7136; longitude = 46.6753 } -Token $mgr -Expect 409 | Out-Null
$venue = Call POST /venue/unpublish -Token $mgr -Expect 200
Call PUT "/venue/courts/$($court2.id)" @{ categoryId = $padel.id; nameEn = 'Court 2'; nameAr = 'ملعب 2'; indoor = $false; featureIds = @(); sortOrder = 2; active = $false } -Token $mgr -Expect 200 | Out-Null

# 5. Roles and venue scoping
Call GET /venue -Token $desk -Expect 200 | Out-Null
Call PUT /venue/hours @() -Token $desk -Expect 403 | Out-Null
Call PUT "/venue/courts/$($court1.id)" @{ categoryId = $padel.id; nameEn = 'Hijack'; nameAr = 'س'; indoor = $true; featureIds = @(); sortOrder = 1; active = $true } -Token $other -Expect 404 | Out-Null
Call GET /venue -Token $op -Expect 403 | Out-Null
Call GET /admin/categories -Token $mgr -Expect 403 | Out-Null

Write-Host "`nFailures: $script:failures"
```

Expected: every line `PASS`, final `Failures: 0`.

- [ ] **Step 3: Check the database**

```powershell
$env:PGPASSWORD = 'midango_dev'
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
& $psql -U midango -h localhost -d midango -c "SELECT name_en, durations_minutes, active FROM sport_categories ORDER BY created_at DESC LIMIT 2;"
& $psql -U midango -h localhost -d midango -c "SELECT day, opens, closes FROM opening_hours ORDER BY venue_id, day LIMIT 6;"
& $psql -U midango -h localhost -d midango -c "INSERT INTO opening_hours (venue_id, day, opens, closes) SELECT id, 6, '10:00', '10:00' FROM venues LIMIT 1;"
Remove-Item Env:PGPASSWORD
```

Expected: football `{60} | f` and padel `{60,90,120} | t`; hours rows `16:00:00 | 02:00:00` (Friday `14:00:00`); the INSERT fails with `violates check constraint "ck_opening_hours_not_empty"`.

- [ ] **Step 4: Stop the API, tick M3a in the roadmap (`Catalog API` → `Catalog API ✅ done`) and commit**

```powershell
Get-NetTCPConnection -LocalPort 5300 -State Listen -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
git add docs/superpowers/plans/2026-11-17-midango-roadmap.md
git commit -m "docs: mark M3a Catalog API done"
```

---

## What M3a deliberately leaves out

- **Photos and descriptions:** these need file uploads. They come with the app venue page (M8) or a small media plan before it.
- **Court closures for a date range and blocked time:** they belong with the schedule grid (M6), which is where staff block time.
- **A public venue search endpoint:** M5/M8, together with availability.
- **Multiple opening periods per day:** wait until a venue needs one.
