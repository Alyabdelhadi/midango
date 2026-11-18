# M9a No-show Limit & Reminders (server side) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Customers who miss bookings are limited (2 no-shows in 30 days → only 1 upcoming booking at a time), the platform operator can change those numbers and the reminder lead time on a **Rules** page, phones can register for push, and a background job sends a "Still coming?" reminder 3 hours before each booking — through Firebase Cloud Messaging when a service-account key is configured, otherwise written to the log.

**Architecture:**
- **Rules in one row.** A `platform_settings` table with a single seeded row holds `ReminderHoursBefore = 3`, `NoShowLimit = 2`, `NoShowWindowDays = 30`, `LimitedUpcomingBookings = 1`. The operator edits it; the booking endpoint and the reminder job read it.
- **The limit is checked where customers book** (`POST /bookings`), across all venues. Desk bookings are not limited — the venue decides who it serves.
- **Devices** are rows of `(customer, FCM token, platform, language)`. One token belongs to one customer: registering a token another customer had moves it.
- **Push behind one interface.** `IPushSender` has two implementations: `FcmPushSender` (FirebaseAdmin SDK, used when `Firebase:CredentialsPath` points at a readable service-account JSON) and `LogPushSender` (used otherwise; logs a masked line). FCM tells us which tokens are dead; we delete them.
- **The reminder job** is a `BackgroundService` that wakes every `Reminders:IntervalSeconds` (60), picks confirmed, not-checked-in customer bookings starting within the lead time whose reminder hasn't been handled, skips those booked after the lead time began (no "Still coming?" for a booking made 20 minutes ago), sends one push per booking in each device's language, and stamps `reminder_sent_at` either way so nothing is sent twice.

**Tech Stack:** .NET 10 · EF Core 10 · `FirebaseAdmin` (NuGet) · React 19 admin.

**Project rules:** No automated tests; Task 4 is a scripted run against the live API plus the API log and SQL. The service-account key is a secret: it lives outside the repo, its path comes from configuration (user secrets or an environment variable), and it is never committed or logged. Every new string in English and Arabic.

**Decisions this plan locks in:**

| Topic | Decision | Why |
|---|---|---|
| Numbers | Reminder 3 h before; 2 no-shows in 30 days → 1 upcoming booking (user-approved 2026-11-18) | Roadmap open item, now confirmed |
| Who edits | Platform operator only, on a Rules page | One policy for the whole marketplace |
| Limit message | 409 "You missed {n} bookings in the last {d} days, so for now you can hold {k} upcoming booking at a time." | The app already shows API titles in the confirm sheet |
| Late booking | No reminder when the booking was made inside the lead time | Pointless and annoying |
| No devices | Reminder marked handled anyway | Don't rescan the same booking every minute |
| Reminder text | EN "Still coming?" / "{court} at {venue}, {time}. Can't make it? Cancel in the app so someone else can play." — AR equivalent; data `{bookingId}` | The app (M9b) opens that booking |
| Firebase project | `yallaplay-a00a3` (user-provided) | Existing project |

## Endpoints delivered

| Method & path | Who | Result |
|---|---|---|
| `GET /admin/settings` | operator | `{reminderHoursBefore, noShowLimit, noShowWindowDays, limitedUpcomingBookings}` |
| `PUT /admin/settings` | operator | same · 400 out of range |
| `PUT /me/devices` `{token, platform, language}` | customer | 204 |
| `DELETE /me/devices?token=…` | customer | 204 (call before signing out) |
| `POST /bookings` | customer | new 409 when limited |

---

### Task 1: Settings, limit, devices, reminder field

**Files:** create `api/src/Midango.Api/Platform/PlatformSettings.cs`, `Platform/SettingsEndpoints.cs`, `Notifications/CustomerDevice.cs`, `Notifications/DeviceEndpoints.cs`; modify `Bookings/Booking.cs`, `Bookings/BookingEndpoints.cs`, `Data/MidangoDb.cs`, `Program.cs`; migration `NoShowRulesAndDevices`.

- [ ] **Step 1: Settings entity** — `Platform/PlatformSettings.cs`:

```csharp
namespace Midango.Api.Platform;

/// Marketplace-wide rules. Exactly one row (Id = 1), seeded by the migration.
public sealed class PlatformSettings
{
    public const int SingletonId = 1;
    public int Id { get; init; } = SingletonId;

    /// "Still coming?" goes out this many hours before the start.
    public int ReminderHoursBefore { get; set; } = 3;

    /// This many no-shows within NoShowWindowDays limits the customer…
    public int NoShowLimit { get; set; } = 2;
    public int NoShowWindowDays { get; set; } = 30;

    /// …to this many upcoming bookings at a time.
    public int LimitedUpcomingBookings { get; set; } = 1;
}
```

- [ ] **Step 2: Device entity** — `Notifications/CustomerDevice.cs`:

```csharp
namespace Midango.Api.Notifications;

/// A phone that can receive push for a customer. The FCM token is unique across customers.
public sealed class CustomerDevice
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; set; }
    public required string Token { get; init; }

    /// "android", "ios" or "web".
    public required string Platform { get; set; }

    /// "en" or "ar": the language reminders are written in.
    public required string Language { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastSeenAt { get; set; }
}
```

- [ ] **Step 3: Booking field.** In `Booking.cs` add after `NoShowAt`:

```csharp
    /// When the "Still coming?" reminder was handled (sent, or skipped: no device, or booked too late for one).
    public DateTimeOffset? ReminderSentAt { get; set; }
```

- [ ] **Step 4: Mapping.** In `MidangoDb.cs` add `DbSet<PlatformSettings> PlatformSettings` and `DbSet<CustomerDevice> CustomerDevices`, and:

```csharp
        b.Entity<PlatformSettings>(e =>
        {
            e.ToTable("platform_settings", t => t.HasCheckConstraint("ck_platform_settings_singleton", "id = 1"));
            e.Property(s => s.Id).ValueGeneratedNever();
            e.HasData(new PlatformSettings());
        });

        b.Entity<CustomerDevice>(e =>
        {
            e.Property(d => d.Token).HasMaxLength(512);
            e.HasIndex(d => d.Token).IsUnique();
            e.Property(d => d.Platform).HasMaxLength(16);
            e.Property(d => d.Language).HasMaxLength(2);
            e.HasOne<Customer>().WithMany().HasForeignKey(d => d.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(d => d.CustomerId);
        });
```

  and in the `Booking` block an index for the reminder scan: `e.HasIndex(x => new { x.Status, x.StartsAt });`.

- [ ] **Step 5: Migration** — `dotnet ef migrations add NoShowRulesAndDevices --project src/Midango.Api` from `api/`. Check `Up`: `platform_settings` created with the seeded row `(1, 3, 2, 30, 1)`, `customer_devices`, `reminder_sent_at`, the new index. Then `dotnet ef database update --project src/Midango.Api`.

- [ ] **Step 6: Settings endpoints** — `Platform/SettingsEndpoints.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Platform;

public static class SettingsEndpoints
{
    public sealed record SettingsDto(int ReminderHoursBefore, int NoShowLimit, int NoShowWindowDays, int LimitedUpcomingBookings)
    {
        public static SettingsDto From(PlatformSettings s) =>
            new(s.ReminderHoursBefore, s.NoShowLimit, s.NoShowWindowDays, s.LimitedUpcomingBookings);
    }

    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin/settings").RequireAuthorization(Policies.PlatformOperator);

        admin.MapGet("", async (MidangoDb db, CancellationToken ct) =>
            Results.Ok(SettingsDto.From(await db.PlatformSettings.AsNoTracking().SingleAsync(ct))));

        admin.MapPut("", async (SettingsDto input, MidangoDb db, CancellationToken ct) =>
        {
            if (input.ReminderHoursBefore is < 1 or > 24) return ApiErrors.Fail(400, "Send the reminder between 1 and 24 hours before the start.");
            if (input.NoShowLimit is < 1 or > 10) return ApiErrors.Fail(400, "The no-show limit must be between 1 and 10.");
            if (input.NoShowWindowDays is < 7 or > 180) return ApiErrors.Fail(400, "Count no-shows over 7 to 180 days.");
            if (input.LimitedUpcomingBookings is < 0 or > 5) return ApiErrors.Fail(400, "Allow between 0 and 5 upcoming bookings while limited.");

            var settings = await db.PlatformSettings.SingleAsync(ct);
            settings.ReminderHoursBefore = input.ReminderHoursBefore;
            settings.NoShowLimit = input.NoShowLimit;
            settings.NoShowWindowDays = input.NoShowWindowDays;
            settings.LimitedUpcomingBookings = input.LimitedUpcomingBookings;
            await db.SaveChangesAsync(ct);
            return Results.Ok(SettingsDto.From(settings));
        });
    }
}
```

- [ ] **Step 7: The limit.** In `BookingEndpoints.cs`, customer `POST /bookings`, right after `var now = clock.GetUtcNow();` and the two time checks that follow it (before loading opening hours), add:

```csharp
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
```

- [ ] **Step 8: Device endpoints** — `Notifications/DeviceEndpoints.cs`:

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Notifications;

public static class DeviceEndpoints
{
    public sealed record DeviceBody(string? Token, string? Platform, string? Language);

    private static readonly string[] Platforms = ["android", "ios", "web"];

    public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/me/devices").RequireAuthorization(Policies.Customer);

        me.MapPut("", async (DeviceBody body, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var token = body.Token?.Trim();
            if (string.IsNullOrEmpty(token) || token.Length > 512) return ApiErrors.Fail(400, "Missing device token.");
            if (body.Platform is not { } platform || !Platforms.Contains(platform)) return ApiErrors.Fail(400, "Unknown platform.");
            var language = body.Language == "ar" ? "ar" : "en";

            var now = clock.GetUtcNow();
            var device = await db.CustomerDevices.SingleOrDefaultAsync(d => d.Token == token, ct);
            if (device is null)
            {
                db.CustomerDevices.Add(new CustomerDevice
                {
                    CustomerId = user.SubjectId(), Token = token, Platform = platform, Language = language,
                    CreatedAt = now, LastSeenAt = now,
                });
            }
            else
            {
                device.CustomerId = user.SubjectId(); // a phone that changed hands follows its new owner
                device.Platform = platform;
                device.Language = language;
                device.LastSeenAt = now;
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        me.MapDelete("", async (string? token, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            await db.CustomerDevices.Where(d => d.Token == token && d.CustomerId == customerId).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
    }
}
```

- [ ] **Step 9: Map** `app.MapSettingsEndpoints();` and `app.MapDeviceEndpoints();` in `Program.cs`. `dotnet build` 0/0. **Commit** `feat(api): no-show limit with operator-editable rules; customer push devices; reminder field`.

---

### Task 2: Push senders and the reminder job

**Files:** create `Notifications/PushSender.cs`, `Notifications/ReminderService.cs`; modify `Program.cs`, `appsettings.json`, `Midango.Api.csproj` (FirebaseAdmin), repo `.gitignore`.

- [ ] **Step 1: Package.** From `api/`: `dotnet add src/Midango.Api package FirebaseAdmin`. It must restore for `net10.0`; if the newest version fails, pin the newest that works and report it.

- [ ] **Step 2: Senders** — `Notifications/PushSender.cs`:

```csharp
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace Midango.Api.Notifications;

public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);

public interface IPushSender
{
    /// Sends one message to each token. Returns the tokens FCM says are no longer valid.
    Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct);
}

/// Used when no Firebase key is configured: the reminder pipeline still runs, pushes go to the log.
public sealed class LogPushSender(ILogger<LogPushSender> log) : IPushSender
{
    public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct)
    {
        foreach (var token in tokens)
            log.LogInformation("PUSH (not sent, no Firebase key) to device …{Tail}: {Title} | {Body}",
                token[^Math.Min(6, token.Length)..], message.Title, message.Body);
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}

public sealed class FcmPushSender : IPushSender
{
    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FcmPushSender> _log;

    public FcmPushSender(string credentialsPath, ILogger<FcmPushSender> log)
    {
        var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
        {
            Credential = CredentialFactory.FromFile<ServiceAccountCredential>(credentialsPath).ToGoogleCredential(),
        });
        _messaging = FirebaseMessaging.GetMessaging(app);
        _log = log;
    }

    public async Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct)
    {
        if (tokens.Count == 0) return [];
        var response = await _messaging.SendEachForMulticastAsync(new MulticastMessage
        {
            Tokens = tokens.ToList(),
            Notification = new Notification { Title = message.Title, Body = message.Body },
            Data = message.Data,
        }, ct);

        var dead = new List<string>();
        for (var i = 0; i < response.Responses.Count; i++)
        {
            var error = response.Responses[i].Exception;
            if (error is null) continue;
            if (error.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument) dead.Add(tokens[i]);
            else _log.LogWarning("Push to a device failed: {Code}", error.MessagingErrorCode);
        }
        return dead;
    }
}
```

  If the installed FirebaseAdmin/Google.Apis version lacks `CredentialFactory`, use `GoogleCredential.FromFile(credentialsPath)` and say so.

- [ ] **Step 3: Reminder job** — `Notifications/ReminderService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Midango.Api.Bookings;
using Midango.Api.Data;

namespace Midango.Api.Notifications;

/// Every minute: sends "Still coming?" for bookings starting within the reminder lead time.
public sealed class ReminderService(IServiceScopeFactory scopes, IPushSender push, TimeProvider clock,
    IConfiguration config, ILogger<ReminderService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(config.GetValue("Reminders:IntervalSeconds", 60));
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Reminder run failed; trying again next interval");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MidangoDb>();
        var rules = await db.PlatformSettings.AsNoTracking().SingleAsync(ct);
        var now = clock.GetUtcNow();
        var lead = TimeSpan.FromHours(rules.ReminderHoursBefore);
        var horizon = now + lead;

        var due = await db.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed && b.Kind == BookingKind.Booking && b.CustomerId != null
                        && b.CheckedInAt == null && b.ReminderSentAt == null && b.StartsAt > now && b.StartsAt <= horizon)
            .ToListAsync(ct);
        if (due.Count == 0) return;

        var courtIds = due.Select(b => b.CourtId).Distinct().ToList();
        var places = await (from c in db.Courts
                            join v in db.Venues on c.VenueId equals v.Id
                            where courtIds.Contains(c.Id)
                            select new { CourtId = c.Id, c.NameEn, c.NameAr, VenueEn = v.NameEn, VenueAr = v.NameAr, v.TimeZoneId })
            .AsNoTracking().ToDictionaryAsync(p => p.CourtId, ct);

        foreach (var booking in due)
        {
            booking.ReminderSentAt = now;
            if (booking.CreatedAt > booking.StartsAt - lead) continue; // booked inside the window: no reminder

            var devices = await db.CustomerDevices.Where(d => d.CustomerId == booking.CustomerId).ToListAsync(ct);
            var place = places[booking.CourtId];
            var time = TimeZoneInfo.ConvertTime(booking.StartsAt, TimeZoneInfo.FindSystemTimeZoneById(place.TimeZoneId)).ToString("HH:mm");
            var data = new Dictionary<string, string> { ["bookingId"] = booking.Id.ToString() };
            foreach (var group in devices.GroupBy(d => d.Language))
            {
                var message = group.Key == "ar"
                    ? new PushMessage("هل ستحضر؟",
                        $"{place.NameAr} في {place.VenueAr}، الساعة {time}. لن تتمكن من الحضور؟ ألغِ من التطبيق ليلعب غيرك.", data)
                    : new PushMessage("Still coming?",
                        $"{place.NameEn} at {place.VenueEn}, {time}. Can't make it? Cancel in the app so someone else can play.", data);
                var dead = await push.SendAsync(group.Select(d => d.Token).ToList(), message, ct);
                if (dead.Count > 0) db.CustomerDevices.RemoveRange(group.Where(d => dead.Contains(d.Token)));
            }
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("Reminders handled for {Count} bookings", due.Count);
    }
}
```

- [ ] **Step 4: Wire it** in `Program.cs` (after the other service registrations):

```csharp
// Push: real FCM when a service-account key is configured (never in the repo), otherwise the log.
var firebaseKey = builder.Configuration["Firebase:CredentialsPath"];
if (!string.IsNullOrWhiteSpace(firebaseKey) && File.Exists(firebaseKey))
    builder.Services.AddSingleton<IPushSender>(sp => new FcmPushSender(firebaseKey, sp.GetRequiredService<ILogger<FcmPushSender>>()));
else
    builder.Services.AddSingleton<IPushSender, LogPushSender>();
builder.Services.AddHostedService<ReminderService>();
```

  Add `"Reminders": { "IntervalSeconds": 60 }` to `appsettings.json`. Add `*firebase*adminsdk*.json` and `secrets/` to the repo's `.gitignore`.

- [ ] **Step 5:** `dotnet build` 0/0. **Commit** `feat(api): "Still coming?" reminders through FCM when configured, the log otherwise`.

---

### Task 3: Rules page for the operator

**Files:** `admin/src/lib/types.ts`, `locales/en.ts`, `locales/ar.ts`, create `pages/RulesPage.tsx`, `App.tsx`, `layout/Shell.tsx`.

- [ ] **Step 1:** Type `PlatformSettings = { reminderHoursBefore: number; noShowLimit: number; noShowWindowDays: number; limitedUpcomingBookings: number }`.
- [ ] **Step 2: Strings** (en / ar):
  - `navRules`: 'Rules' / 'القواعد'
  - `rulesIntro`: 'These apply to every venue and every customer.' / 'تنطبق هذه القواعد على جميع المنشآت وجميع العملاء.'
  - `reminderHours`: 'Send "Still coming?" this many hours before' / 'إرسال «هل ستحضر؟» قبل البدء بعدد الساعات'
  - `noShowLimit`: 'No-shows that limit a customer' / 'عدد مرات الغياب التي تقيّد العميل'
  - `noShowWindow`: 'Counted over the last (days)' / 'خلال آخر (يوماً)'
  - `limitedUpcoming`: 'Upcoming bookings a limited customer may hold' / 'عدد الحجوزات القادمة المسموحة للعميل المقيّد'
  - `rulesSummary`: '{n} no-shows in {d} days → at most {k} upcoming booking(s).' / '{n} حالات غياب خلال {d} يوماً ← {k} حجز قادم كحد أقصى.'
- [ ] **Step 3: Page** `pages/RulesPage.tsx`: loads `GET /admin/settings`; a form (`form-grid`) with four `type="number"` fields (min/max as the API: 1–24, 1–10, 7–180, 0–5), the live summary line under them, Save → `PUT /admin/settings` → success toast (`useToast`) / error toast with the API title. Follow the `PricesPage` / `VenuePage` conventions (`useLoad`, `Field`, `Button`, `Notice`, `PageHeader`).
- [ ] **Step 4:** Route `rules` for `PlatformOperator` only; nav item after Venues with Lucide `ShieldCheck`.
- [ ] **Step 5:** `npm run build` + `npm run lint` (0 errors). **Commit** `feat(admin): Rules page for the no-show limit and reminder timing`.

---

### Task 4: Verify (controller)

- [ ] **Step 1:** Start the API with `Reminders__IntervalSeconds=5` and its log captured, no Firebase key.
- [ ] **Step 2: Script** checks:
  - Operator reads and edits settings; out-of-range values get 400; a venue manager gets 403.
  - Limit: the desk books two in-progress bookings for a customer's phone and marks both no-show. That customer can then book one upcoming booking in the app, and a second gets the 409 sentence. A desk booking for the same customer still works. With `noShowLimit = 3` the second app booking works.
  - Devices: `PUT` → 204; registering the same token for a second customer moves it; `DELETE` → 204.
  - Reminder: a customer booking made 2 days ahead, then moved by SQL to start in 2 hours with `created_at` a day ago, gets a `PUSH (not sent…)` line in the API log within ~10 s, in the device's language, and `reminder_sent_at` is set. A booking whose `created_at` is inside the window gets `reminder_sent_at` but no PUSH line. A checked-in booking gets neither.
- [ ] **Step 3: Browser:** operator Rules page in English and Arabic; save; reload keeps the values.
- [ ] **Step 4:** Restore the settings to 3 / 2 / 30 / 1; re-run the M5 script (`Failures: 0`); tick M9a in the roadmap; commit; merge.

## Needs the user before push really arrives (M9b)

1. **Service-account key:** Firebase console → project `yallaplay-a00a3` → Project settings → Service accounts → *Generate new private key*. Save it **outside the repo** (e.g. `C:\Users\Lenovo\secrets\midango-firebase.json`), then from `api/src/Midango.Api` run `dotnet user-secrets set Firebase:CredentialsPath <path>` (or set the `Firebase__CredentialsPath` environment variable).
2. **App registration:** `firebase login` (interactive, once), then `flutterfire configure --project=yallaplay-a00a3` adds the Android/iOS/web app configs. M9b then adds `firebase_messaging` to the Flutter app: permission prompt after the first booking, `PUT /me/devices` on sign-in and token refresh, `DELETE` before sign-out, and opening the booking when a reminder is tapped.

## Verification results

- API script (reminder interval 5 s, no Firebase key): `Failures: 0` — seeded rules, 400s, 403 for a manager; two desk no-shows → second app booking refused with the exact sentence, desk booking still allowed, limit 3 lets it through; device token moves between customers and only its owner can delete it; the due booking's English reminder appears once in the log, a booking made inside the window and a checked-in one get none; `reminder_sent_at` stamped as expected.
- Browser: operator sees Rules (only operator), saves, reload keeps the value, Arabic RTL; settings restored to 3 / 2 / 30 / 1.
- Fixed: the four Rules fields were misaligned when labels wrapped (`.rules-grid { align-items: end }`).
- **Found by the M5 re-run:** the 5-way race once returned 500s — PostgreSQL `40P01 deadlock detected` between simultaneous overlapping inserts through the exclusion constraint. `BookingStore.SaveAsync` now retries a deadlock victim (which then gets the normal 409 or succeeds). After the fix: 3 × 5-way and 2 × 20-way races all clean (1 × 201, rest 409, no 500s). The deadlock did not recur in those runs, so the retry branch itself is not yet exercised live.
- Script lesson: ProblemDetails bodies (`application/problem+json`) come back from `Invoke-WebRequest` as bytes — decode them before checking titles.
