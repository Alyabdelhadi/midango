# M8 MIDANGO App: Booking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A customer opens the Flutter app, picks a sport, a day and a length, sees venues with their open times, opens a venue, taps a time, confirms "pay at the venue" (signing in with a phone code only at that moment), and gets a confirmation with a QR code for the desk. A Bookings tab lists upcoming and past bookings and can cancel; a Profile tab holds the name, the language (English / Arabic) and sign-out.

**Architecture:**
- **API (Task 1):** two public read endpoints for discovery — `GET /venues` (venue cards with open times for a sport/day/length) and `GET /venues/{id}` (venue page) — built on the M5 availability calculation, which is extracted into a reusable method. Plus CORS for the app's web build in development only.
- **App:** plain Flutter, no state-management package. One `Session` (`ChangeNotifier`) owns tokens and the profile; one `ApiClient` adds the bearer token and refreshes it once on a 401 through a single shared in-flight future (refresh tokens are single-use — the M3b lesson). Strings are a typed Dart class with an English and an Arabic instance; a missing Arabic string is a compile error. Venue times are read straight from the API's ISO strings (`2026-11-19T20:00:00+03:00` → "20:00"), as in the admin.
- **The QR code is the bare 6-character reference** — the M7 desk's scanner types it into the reference box.

**Tech Stack:** .NET 10 · Flutter 3.47.7 (`C:\Users\Lenovo\flutter\bin\flutter.bat`, run from **PowerShell only** — it hangs under Git Bash) · `http` · `flutter_secure_storage` · `qr_flutter` · existing `google_fonts`, `lucide_icons_flutter`, `flutter_localizations`.

**Project rules:** No automated tests (`flutter analyze` must be clean; Task 6 is a scripted API run plus a browser walkthrough of the web build). Every visible string comes from the `Strings` class, in English and Arabic. Brand: lime fill with navy text only; links `MidangoColors.cyanInk`; statuses in words. Western digits in both languages.

**Decisions this plan locks in:**

| Topic | Decision | Why |
|---|---|---|
| Guest browsing | Play tab and venue pages need no account; sign-in happens at "Book" | Research: don't gate browsing |
| Sign-in | Phone → 6-digit code → name (only if the account has none) → booking continues | M2 flow; desk uses the name |
| Token storage | Refresh token in `flutter_secure_storage`; access token in memory | Keystore/Keychain on devices |
| Venue order | Venues with free times first, then by name. No distance yet | No location permission in v1 |
| Card times | First 8 free start times for the chosen day and length; tapping one opens the confirm sheet directly | Fewest taps to book |
| Court choice | Confirm sheet offers the free courts at that time, cheapest first, preselected | Most players don't care which court |
| Payment | "Pay at the venue (cash)" shown on confirm, confirmation and booking detail | Q3/Q4 |
| QR content | The bare reference, e.g. `K7Q2MX` | M7 desk lookup |
| Date strip | Today + 13 days, device date | Riyadh launch; API refuses out-of-range days |
| API base URL | `--dart-define=API_BASE_URL=…`, default `http://localhost:5300` | Web build and `adb reverse` in dev |
| CORS | Only in Development, only listed origins (`http://localhost:5400`) | The phone app never needs CORS |

## Endpoints delivered (public)

| Method & path | Result |
|---|---|
| `GET /venues?categoryId=&date=YYYY-MM-DD&minutes=` | `[{id, nameEn, nameAr, addressEn, addressAr, latitude, longitude, currency, courts, fromTotal, open, times[{start, localTime, courts[{courtId, total}]}]}]` · 400 bad length |
| `GET /venues/{id}` | `{id, nameEn, nameAr, addressEn, addressAr, latitude, longitude, currency, vatPercent, freeCancellationHours, hours[{day, opens, closes}], categories[{id, nameEn, nameAr, iconKey, durationsMinutes}], courts[{id, categoryId, nameEn, nameAr, indoor, features[{nameEn, nameAr}]}]}` · 404 unpublished |

## App file structure (`app/lib/`)

| File | Responsibility |
|---|---|
| `main.dart` | `MidangoApp`: theme, locale (restored from storage), `Session` restore, `HomeShell` |
| `config.dart` | `apiBase` from `--dart-define` |
| `api/api_client.dart` | JSON over `http`, bearer token, one refresh retry, `ApiException(status, title)` |
| `api/models.dart` | Plain immutable classes with `fromJson` for categories, venue cards, venue detail, availability, bookings, me |
| `session/session.dart` | `Session` (`ChangeNotifier`): restore, request code, verify, update name, refresh, sign out |
| `l10n/strings.dart` | `Strings` class + `en` / `ar` instances + `Strings.of(context)` via an `InheritedWidget` |
| `l10n/format.dart` | `clock(iso)`, `dayOf(iso)`, `formatDay(date, strings)`, `money(amount, currency, strings)` |
| `home_shell.dart` | `NavigationBar` with Play / Bookings / Profile |
| `play/play_tab.dart` | Sport chips, date strip, length chips, venue cards |
| `play/pickers.dart` | Sport chips, date strip and length chips, shared by Play and the venue page |
| `play/venue_card.dart` | Card with name, address, "from" price, time chips |
| `venue/venue_page.dart` | Venue details, hours, courts, date + length + all times |
| `booking/confirm_sheet.dart` | Bottom sheet: summary, court choice, price, pay-at-venue, free-cancellation line, Book |
| `booking/sign_in_flow.dart` | Phone page → code page → name page; returns true when signed in with a name |
| `booking/confirmation_page.dart` | "You're booked", reference, QR, details, pay-at-venue amount |
| `bookings/bookings_tab.dart` | Upcoming / Past lists; sign-in prompt when signed out |
| `bookings/booking_detail_page.dart` | QR, details, status, Cancel with confirm dialog |
| `profile/profile_tab.dart` | Name (editable), phone, language switch, sign out / sign in |
| `widgets/slot_chip.dart` | Existing; reused |
| `widgets/sport_icon.dart` | `iconKey` → Lucide icon, default `LucideIcons.trophy` |

The styleguide stays in the repo but is no longer the home screen.

---

### Task 1: Discovery API

**Files:** `Bookings/AvailabilityEndpoints.cs` (extract `ComputeAsync`), create `Catalog/PublicVenueEndpoints.cs`, `Program.cs` (map + CORS), `appsettings.Development.json`.

- [ ] **Step 1: Extract the calculation.** In `AvailabilityEndpoints`, move everything after the validation (from `var hours = …` to the final `Results.Ok`) into:

```csharp
    /// Start times of one business day for one sport and length, each with its free courts and their prices.
    /// Past times are left out. Callers validate the category, length and date first.
    internal static async Task<AvailabilityDto> ComputeAsync(
        MidangoDb db, Venue venue, Guid categoryId, DateOnly date, int minutes, DateTimeOffset now, CancellationToken ct)
```

  It returns the `AvailabilityDto` the endpoint returned before (open = false with no slots when the day has no start times). The endpoint becomes: validation as today, then `return Results.Ok(await ComputeAsync(db, venue, categoryId, date, minutes, clock.GetUtcNow(), ct));`. Behaviour must not change (the M5 script proves it).

- [ ] **Step 2: Public venue endpoints.** Create `Catalog/PublicVenueEndpoints.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Midango.Api.Bookings;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Catalog;

/// What the app shows before anyone signs in: venue cards with open times, and a venue page.
public static class PublicVenueEndpoints
{
    public const int CardTimes = 8;

    public sealed record VenueCardDto(
        Guid Id, string NameEn, string NameAr, string? AddressEn, string? AddressAr, double? Latitude, double? Longitude,
        string Currency, int Courts, decimal? FromTotal, bool Open, IReadOnlyList<AvailabilityEndpoints.SlotDto> Times);

    public sealed record PublicFeatureDto(string NameEn, string NameAr);

    public sealed record PublicCourtDto(
        Guid Id, Guid CategoryId, string NameEn, string NameAr, bool Indoor, IReadOnlyList<PublicFeatureDto> Features);

    public sealed record PublicCategoryDto(Guid Id, string NameEn, string NameAr, string IconKey, int[] DurationsMinutes);

    public sealed record PublicVenueDto(
        Guid Id, string NameEn, string NameAr, string? AddressEn, string? AddressAr, double? Latitude, double? Longitude,
        string Currency, decimal VatPercent, int FreeCancellationHours,
        IReadOnlyList<VenueEndpoints.HoursDto> Hours, IReadOnlyList<PublicCategoryDto> Categories, IReadOnlyList<PublicCourtDto> Courts);

    public static void MapPublicVenueEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/venues", async (
            Guid categoryId, DateOnly date, int minutes, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var category = await db.SportCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == categoryId && c.Active, ct);
            if (category is null || !category.DurationsMinutes.Contains(minutes))
                return ApiErrors.Fail(400, "Choose one of this sport's booking lengths.");

            var venues = await db.Venues.AsNoTracking()
                .Where(v => v.Published && db.Courts.Any(c => c.VenueId == v.Id && c.CategoryId == categoryId && c.Active))
                .ToListAsync(ct);
            var courtCounts = await db.Courts.AsNoTracking()
                .Where(c => c.CategoryId == categoryId && c.Active)
                .GroupBy(c => c.VenueId).Select(g => new { VenueId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.VenueId, g => g.Count, ct);

            var now = clock.GetUtcNow();
            var cards = new List<VenueCardDto>();
            // ponytail: one availability calculation per venue, in sequence. Fine for tens of venues;
            // precompute per day or page the list when a city has hundreds.
            foreach (var venue in venues)
            {
                var today = DateOnly.FromDateTime(VenueClock.ToLocal(now, VenueClock.Zone(venue)));
                var inRange = date >= today && date <= today.AddDays(Slots.MaxDaysAhead);
                var day = inRange ? await AvailabilityEndpoints.ComputeAsync(db, venue, categoryId, date, minutes, now, ct) : null;
                var free = day?.Slots.Where(s => s.Courts.Count > 0).ToList() ?? [];
                cards.Add(new VenueCardDto(
                    venue.Id, venue.NameEn, venue.NameAr, venue.AddressEn, venue.AddressAr, venue.Latitude, venue.Longitude,
                    venue.CurrencyCode, courtCounts.GetValueOrDefault(venue.Id),
                    free.Count == 0 ? null : free.SelectMany(s => s.Courts).Min(c => c.Total),
                    day?.Open ?? false, free.Take(CardTimes).ToList()));
            }

            return Results.Ok(cards.OrderByDescending(c => c.Times.Count > 0).ThenBy(c => c.NameEn));
        });

        app.MapGet("/venues/{id:guid}", async (Guid id, MidangoDb db, CancellationToken ct) =>
        {
            var venue = await db.Venues.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id && v.Published, ct);
            if (venue is null) return ApiErrors.Fail(404, "Venue not found.");

            var hours = await db.OpeningHours.AsNoTracking().Where(h => h.VenueId == id).OrderBy(h => h.Day).ToListAsync(ct);
            var courts = await db.Courts.AsNoTracking().Include(c => c.Features)
                .Where(c => c.VenueId == id && c.Active).OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);
            var categoryIds = courts.Select(c => c.CategoryId).Distinct().ToList();
            var categories = await db.SportCategories.AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id) && c.Active).OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);
            var shown = categories.Select(c => c.Id).ToHashSet();

            return Results.Ok(new PublicVenueDto(
                venue.Id, venue.NameEn, venue.NameAr, venue.AddressEn, venue.AddressAr, venue.Latitude, venue.Longitude,
                venue.CurrencyCode, venue.VatPercent, venue.FreeCancellationHours,
                hours.Select(h => new VenueEndpoints.HoursDto(h.Day, h.Opens, h.Closes)).ToList(),
                categories.Select(c => new PublicCategoryDto(c.Id, c.NameEn, c.NameAr, c.IconKey, c.DurationsMinutes)).ToList(),
                courts.Where(c => shown.Contains(c.CategoryId))
                    .Select(c => new PublicCourtDto(c.Id, c.CategoryId, c.NameEn, c.NameAr, c.Indoor,
                        c.Features.Select(f => new PublicFeatureDto(f.NameEn, f.NameAr)).ToList()))
                    .ToList()));
        });
    }
}
```

- [ ] **Step 3: CORS for the app's web build (Development only).** In `Program.cs`, after `AddProblemDetails()`:

```csharp
// The Flutter web build calls the API from another origin in development. Mobile apps don't use CORS.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));
```

  and `app.UseCors();` right after `app.UseExceptionHandler();`. Add to `appsettings.Development.json`: `"Cors": { "Origins": [ "http://localhost:5400" ] }`. Map the new endpoints: `app.MapPublicVenueEndpoints();` after `app.MapAvailabilityEndpoints();`.

- [ ] **Step 4:** `dotnet build` → 0 warnings, 0 errors. **Commit** `feat(api): public venue cards with open times and venue page; dev CORS for the app's web build`.

---

### Task 2: App foundation — packages, config, API client, session, strings, shell

- [ ] **Step 1: Packages** (PowerShell, from `app/`): `& C:\Users\Lenovo\flutter\bin\flutter.bat pub add http flutter_secure_storage qr_flutter`. Let pub pick versions compatible with the SDK; report them.

- [ ] **Step 2: Config** — `lib/config.dart`:

```dart
/// Where the API lives. Override per build: --dart-define=API_BASE_URL=http://10.0.2.2:5300
const apiBase = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://localhost:5300');
```

- [ ] **Step 3: API client** — `lib/api/api_client.dart`:

```dart
import 'dart:convert';

import 'package:http/http.dart' as http;

import '../config.dart';

/// A failed call. `title` is the API's sentence for people (ProblemDetails.title).
class ApiException implements Exception {
  ApiException(this.status, this.title);
  final int status;
  final String title;
  @override
  String toString() => title;
}

/// Supplies and renews the access token; implemented by Session.
abstract interface class TokenSource {
  String? get accessToken;

  /// Spends the refresh token once. Concurrent callers must share one call (refresh tokens are single-use).
  Future<bool> refresh();
}

class ApiClient {
  ApiClient({http.Client? client}) : _http = client ?? http.Client();
  final http.Client _http;
  TokenSource? tokens;

  Future<dynamic> get(String path, {Map<String, String>? query, bool auth = false}) =>
      _send('GET', path, query: query, auth: auth);
  Future<dynamic> post(String path, [Object? body, bool auth = false]) => _send('POST', path, body: body, auth: auth);
  Future<dynamic> put(String path, Object? body, {bool auth = true}) => _send('PUT', path, body: body, auth: auth);

  Future<dynamic> _send(String method, String path,
      {Map<String, String>? query, Object? body, required bool auth}) async {
    Future<http.Response> send() {
      final request = http.Request(method, Uri.parse('$apiBase$path').replace(queryParameters: query))
        ..headers['Content-Type'] = 'application/json';
      final token = tokens?.accessToken;
      if (auth && token != null) request.headers['Authorization'] = 'Bearer $token';
      if (body != null) request.body = jsonEncode(body);
      return _http.send(request).then(http.Response.fromStream);
    }

    var res = await send();
    if (res.statusCode == 401 && auth && tokens != null && await tokens!.refresh()) res = await send();
    final text = utf8.decode(res.bodyBytes);
    final json = text.isEmpty ? null : jsonDecode(text);
    if (res.statusCode >= 400) {
      final title = json is Map && json['title'] is String ? json['title'] as String : 'Request failed (${res.statusCode})';
      throw ApiException(res.statusCode, title);
    }
    return json;
  }
}
```

  Auth calls pass `auth: true` (bookings, me). Public calls don't send the token at all.

- [ ] **Step 4: Models** — `lib/api/models.dart`: immutable classes with `factory X.fromJson(Map<String, dynamic>)` for: `Category {id, nameEn, nameAr, iconKey, durationsMinutes}`; `SlotCourt {courtId, total}`; `Slot {start, localTime, courts}`; `VenueCard {id, nameEn, nameAr, addressEn?, addressAr?, currency, courts, fromTotal?, open, times}`; `VenueDetail {id, nameEn, nameAr, addressEn?, addressAr?, latitude?, longitude?, currency, vatPercent, freeCancellationHours, hours[{day, opens, closes}], categories[Category], courts[{id, categoryId, nameEn, nameAr, indoor, features[{nameEn, nameAr}]}]}`; `Availability {date, open, currency, slots}`; `Booking {id, reference, venueId, venueNameEn, venueNameAr, courtId, courtNameEn, courtNameAr, startsAt, endsAt, minutes, total, net, vat, currency, paymentMethod, status, freeCancellationUntil, lateCancellation, cancelledAt?, cancelledByVenue}`; `MyBookings {upcoming, history}`; `Me {id, phone, name?}`. Numbers: `(json['total'] as num).toDouble()`. Keep the ISO strings as `String` (don't parse to `DateTime` for display). A small `Named` interface (`nameEn`, `nameAr`) with an extension `String name(bool arabic)` keeps bilingual names one call away.

- [ ] **Step 5: Session** — `lib/session/session.dart`:

```dart
import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../api/api_client.dart';
import '../api/models.dart';

/// The signed-in customer. Refresh token in secure storage; access token in memory only.
class Session extends ChangeNotifier implements TokenSource {
  Session(this.api, {FlutterSecureStorage? storage}) : _storage = storage ?? const FlutterSecureStorage() {
    api.tokens = this;
  }

  static const _refreshKey = 'refresh_token';
  final ApiClient api;
  final FlutterSecureStorage _storage;
  String? _access;
  String? _refresh;
  Future<bool>? _refreshing;
  Me? me;

  bool get signedIn => _refresh != null;
  bool get hasName => me?.name != null;
  @override
  String? get accessToken => _access;

  /// On launch: trade the stored refresh token for a session and load the profile. Offline keeps it for later.
  Future<void> restore() async {
    _refresh = await _storage.read(key: _refreshKey);
    if (_refresh != null && await refresh()) await loadMe();
    notifyListeners();
  }

  Future<void> requestCode(String phone) => api.post('/auth/otp/request', {'phone': phone});

  /// Returns true when the account already has a name.
  Future<bool> verify(String phone, String code) async {
    final json = await api.post('/auth/otp/verify', {'phone': phone, 'code': code}) as Map<String, dynamic>;
    await _store(json['accessToken'] as String, json['refreshToken'] as String);
    await loadMe();
    notifyListeners();
    return json['profileComplete'] as bool;
  }

  Future<void> loadMe() async {
    me = Me.fromJson(await api.get('/me', auth: true) as Map<String, dynamic>);
  }

  Future<void> saveName(String name) async {
    me = Me.fromJson(await api.put('/me', {'name': name}) as Map<String, dynamic>);
    notifyListeners();
  }

  @override
  Future<bool> refresh() => _refreshing ??= _doRefresh().whenComplete(() => _refreshing = null);

  Future<bool> _doRefresh() async {
    final token = _refresh;
    if (token == null) return false;
    try {
      final json = await api.post('/auth/refresh', {'refreshToken': token}) as Map<String, dynamic>;
      await _store(json['accessToken'] as String, json['refreshToken'] as String);
      return true;
    } on ApiException catch (e) {
      if (e.status == 401) await _clear(); // revoked or expired: signed out
      return false;
    } catch (_) {
      return false; // offline: keep the token and try again later
    }
  }

  Future<void> signOut() async {
    final token = _refresh;
    await _clear();
    notifyListeners();
    if (token != null) unawaited(api.post('/auth/logout', {'refreshToken': token}).catchError((_) => null));
  }

  Future<void> _store(String access, String refresh) async {
    _access = access;
    _refresh = refresh;
    await _storage.write(key: _refreshKey, value: refresh);
  }

  Future<void> _clear() async {
    _access = null;
    _refresh = null;
    me = null;
    await _storage.delete(key: _refreshKey);
  }
}
```

  Provide it with an `InheritedNotifier<Session>` (`SessionScope.of(context)`), created once in `main.dart`.

- [ ] **Step 6: Strings** — `lib/l10n/strings.dart`: a `Strings` class whose constructor takes **every** string as a `required` named parameter (plain `String` fields; strings with values are `String Function(...)` fields, e.g. `final String Function(String ref) bookedRef;`). Two `final` instances, `en` and `ar`, written out in full, plus `weekdays` (Monday first, 7 entries) and `months` (12) lists in each language. `Strings.of(context)` reads a `StringsScope` `InheritedWidget` above the navigator; `isArabic` flag on the instance. Cover every visible text of Tasks 2–5 (tab labels, Play headings, "from {price}", "No open times on this day", "Closed on this day", venue sections, confirm sheet lines, pay-at-venue, free-cancellation line, "Cancelling now is free" / "Cancelling now counts as a late cancellation", sign-in pages, "Resend code", confirmation headline "You're booked", "Show this code at the desk", bookings tab headings, statuses (Confirmed, Cancelled, Cancelled by the venue, Late cancellation, No-show), cancel dialog, profile labels, language names "English" / "العربية", sign-in prompts, generic "Something went wrong. Try again." and "Try again"). Arabic must be natural Modern Standard Arabic (match the admin's tone: "نقداً في المنشأة", "حجز", "إلغاء").

- [ ] **Step 7: Formatting** — `lib/l10n/format.dart`:

```dart
import 'strings.dart';

/// Venue times arrive with the venue's offset; the wall-clock part is read straight from the string.
String clock(String iso) => iso.substring(11, 16);
String dayOf(String iso) => iso.substring(0, 10);

/// "Thu 19 Nov" / "الخميس 19 نوفمبر" — Gregorian, Western digits.
String formatDay(String date, Strings s) {
  final d = DateTime.parse(date);
  return '${s.weekdays[d.weekday - 1]} ${d.day} ${s.months[d.month - 1]}';
}

/// "SAR 240.00" / "240.00 ر.س."
String money(double amount, String currency, Strings s) =>
    s.isArabic ? '${amount.toStringAsFixed(2)} ${currency == 'SAR' ? 'ر.س.' : currency}' : '$currency ${amount.toStringAsFixed(2)}';

String isoDate(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
```

  Time ranges in the Arabic UI are wrapped in `Directionality(textDirection: TextDirection.ltr, child: Text('20:00–21:30'))` (or a `\u2066…\u2069` isolate) so they don't reverse.

- [ ] **Step 8: Shell and main.** `main.dart` builds `ApiClient` + `Session`, restores the session and the saved language (`flutter_secure_storage` key `lang`), and renders `MaterialApp` with `locale`, `supportedLocales` en/ar, `GlobalMaterialLocalizations.delegates`, `StringsScope`, `SessionScope`, home `HomeShell`. `HomeShell` = `Scaffold` with an `IndexedStack` of `PlayTab`, `BookingsTab`, `ProfileTab` and a Material 3 `NavigationBar` (Lucide icons `LucideIcons.calendarPlus`, `LucideIcons.ticket`, `LucideIcons.user`; selected indicator lime with navy icon). Language change is a callback down to `ProfileTab`. Until Tasks 3–5 exist, the three tabs may be placeholder widgets.

- [ ] **Step 9:** `flutter analyze` → "No issues found!". **Commit** `feat(app): API client, session with secure refresh token, EN/AR strings, tab shell`.

---

### Task 3: Play tab and venue page

- [ ] **Step 1: Play tab** (`play/play_tab.dart`): loads `/categories` once.
  - Top: title "Play"; horizontal sport chips (icon from `sport_icon.dart` + name; selected = lime fill, navy text), first sport preselected.
  - Date strip: 14 chips "Today", "Tomorrow", then `formatDay`; default today.
  - Length chips from the sport's `durationsMinutes` ("60 min" / "60 دقيقة"); default the first.
  - Then `GET /venues?categoryId&date&minutes` → list of `VenueCard`s. Loading: a few grey placeholder cards. Empty list: "No venues for this sport yet." Error: message + "Try again".
  - Pull to refresh.
- [ ] **Step 2: Venue card** (`play/venue_card.dart`): name (titleMedium), address (bodySmall, muted), "from SAR 150.00" when `fromTotal` set, then a `Wrap` of up to 8 `SlotChip`s (time, available) — or "No open times on this day" / "Closed on this day". Tapping the card opens `VenuePage(venueId, categoryId, date, minutes)`; tapping a time opens the confirm sheet for that slot (Task 4), fetching the venue detail first if the sheet needs it.
- [ ] **Step 3: Venue page** (`venue/venue_page.dart`): `GET /venues/{id}` + `GET /venues/{id}/availability?date&categoryId&minutes`. Shows name, address, the sport chips this venue offers (only `categories` from the detail), the date strip and length chips (shared from `play/pickers.dart`), every start time as a `SlotChip` (full times greyed and struck through — the M5 decision), then "Courts" (name, indoor/outdoor, features) and "Opening hours" (7 rows, "Closed" for missing days). Tapping a free time opens the confirm sheet.
- [ ] **Step 4:** `flutter analyze` clean. **Commit** `feat(app): Play tab with sports, days, lengths and venue cards; venue page with all times`.

---

### Task 4: Confirm, sign in, book, confirmation

- [ ] **Step 1: Confirm sheet** (`booking/confirm_sheet.dart`): `showModalBottomSheet` (scroll-controlled, safe-area). Shows venue name, `formatDay` + time range (LTR-isolated), length, court chips for the free courts of that slot (cheapest first, preselected; each shows its total when totals differ), the total in `titleLarge`, "Includes VAT", a pay-at-venue row (Lucide `banknote` icon + "Pay at the venue, in cash"), and the free-cancellation line "Free cancellation up to {n} hours before the start" (from the venue's `freeCancellationHours`). Primary button "Book · SAR 240.00".
- [ ] **Step 2: Book.** On "Book": if not signed in → push `SignInFlow`; if it returns false, stay on the sheet. If signed in without a name → the flow's name page. Then `POST /bookings {courtId, start, minutes}` with auth. 201 → close the sheet and push `ConfirmationPage`. 409 → show the API title inside the sheet ("Someone just booked this time. Pick another one.") and refresh the caller's times. Other errors → the title inside the sheet. The button shows a spinner while busy and can't be double-tapped.
- [ ] **Step 3: Sign-in flow** (`booking/sign_in_flow.dart`), pages pushed on the root navigator, returns `bool`:
  1. Phone: one field (`TextInputType.phone`, LTR), hint "05x xxx xxxx", "Send code". Errors show the API title.
  2. Code: 6-digit field (`autofillHints: [AutofillHints.oneTimeCode]`, numeric), "Verify", "Resend code" (disabled for 60 s with a countdown). Wrong code → the API title.
  3. Name (only when `verify` returned false): "What should the venue call you?" + field + "Save".
  Also reachable from the Bookings and Profile tabs' "Sign in" buttons.
- [ ] **Step 4: Confirmation** (`booking/confirmation_page.dart`): headline "You're booked" (`headlineLarge`), the reference in large Space Grotesk letters with letter spacing, a `QrImageView(data: booking.reference, size: 200)` on white with "Show this code at the desk", then venue, day + time range, court, total, "Pay SAR 240.00 at the venue, in cash", the free-cancellation line (from the booking's `freeCancellationUntil`: `formatDay(dayOf(..))` + `clock(..)`). Buttons: "View my bookings" (switches the shell to the Bookings tab) and "Done" (back to Play, which reloads its times).
- [ ] **Step 5:** `flutter analyze` clean. **Commit** `feat(app): confirm sheet, sign in at book time, booking confirmation with QR`.

---

### Task 5: Bookings and Profile

- [ ] **Step 1: Bookings tab** (`bookings/bookings_tab.dart`): signed out → short text + "Sign in". Signed in → `GET /me/bookings` with two sections, "Upcoming" and "Past"; each row: venue name, `formatDay` + time range, court, total, and a status label in words for anything not plain confirmed (Cancelled, Cancelled by the venue, Late cancellation, No-show). Empty upcoming: "No upcoming bookings." with a "Find a court" button to the Play tab. Reloads when the tab is shown and on pull-to-refresh.
- [ ] **Step 2: Booking detail** (`bookings/booking_detail_page.dart`): QR + reference (upcoming confirmed only), details as on the confirmation, status. "Cancel booking" for upcoming confirmed bookings → dialog that says "Cancelling now is free." when now is before `freeCancellationUntil`, otherwise "Cancelling now counts as a late cancellation."; confirm → `POST /bookings/{id}/cancel`; 409 shows the API title (e.g. "You've already checked in at the venue.").
- [ ] **Step 3: Profile tab** (`profile/profile_tab.dart`): signed out → "Sign in" + language switch. Signed in → name (tap to edit, same page as the flow's name page), phone (the customer's own number, shown in full, LTR), language switch (segmented English / العربية, saved), "Sign out".
- [ ] **Step 4:** `flutter analyze` clean; `flutter build web --dart-define=API_BASE_URL=http://localhost:5300` succeeds. **Commit** `feat(app): bookings with detail and cancel; profile with name, language and sign-out`.

---

### Task 6: Verify (controller)

- [ ] **Step 1: API script** (API running with its log captured):

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
    $short = if ($text.Length -gt 140) { $text.Substring(0, 140) + '...' } else { $text }
    Write-Host "$mark $Method $Path -> $([int]$r.StatusCode) (expected $Expect) $short"
    if ($text) { try { return $text | ConvertFrom-Json -DateKind String } catch { return $null } }
}
function Check([bool]$Condition, [string]$What) {
    if ($Condition) { Write-Host "PASS $What" } else { $script:failures++; Write-Host "FAIL $What" }
}

$op = (Call POST /staff/auth/login @{ email = 'operator@midango.local'; password = 'Operator!2026dev' } -Expect 200).accessToken
$s = Get-Random
$padel = Call POST /admin/categories @{ nameEn = "Padel $s"; nameAr = 'بادل'; iconKey = 'volleyball'; durationsMinutes = @(60, 90); sortOrder = 1; active = $true; features = @(@{ nameEn = 'Panoramic'; nameAr = 'بانورامي' }) } -Token $op -Expect 201
$day = [DateTime]::UtcNow.AddHours(3).Date.AddDays(2).ToString('yyyy-MM-dd')

function NewVenue([string]$Name, [bool]$Publish, [decimal]$Price) {
    $v = Call POST /admin/venues @{ nameEn = $Name; nameAr = 'منشأة' } -Token $op -Expect 201
    $email = "pv-$(Get-Random)@arena.test"
    Call POST "/admin/venues/$($v.id)/staff" @{ email = $email; displayName = 'M'; role = 'VenueManager'; temporaryPassword = 'Temp-pass-123' } -Token $op -Expect 201 | Out-Null
    $m = (Call POST /staff/auth/login @{ email = $email; password = 'Temp-pass-123' } -Expect 200).accessToken
    Call PUT /venue @{ nameEn = $Name; nameAr = 'منشأة'; addressEn = 'Riyadh'; addressAr = 'الرياض'; latitude = 24.7; longitude = 46.7 } -Token $m -Expect 200 | Out-Null
    $week = 'Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday' | ForEach-Object { @{ day = $_; opens = '16:00:00'; closes = '23:00:00' } }
    Call PUT /venue/hours $week -Token $m -Expect 200 | Out-Null
    $venue = Call POST /venue/courts @{ categoryId = $padel.id; nameEn = 'Court 1'; nameAr = 'ملعب 1'; indoor = $true; hourlyPrice = $Price; featureIds = @($padel.features[0].id); sortOrder = 1; active = $true } -Token $m -Expect 201
    if ($Publish) { Call POST /venue/publish -Token $m -Expect 200 | Out-Null }
    return @{ id = $v.id; token = $m; court = $venue.courts[0].id }
}
$a = NewVenue "Alpha $s" $true 100
$b = NewVenue "Bravo $s" $true 200
$hidden = NewVenue "Hidden $s" $false 100

# Fill Bravo's whole day with a block so it has no free times.
Call POST /venue/blocks @{ courtIds = @($b.court); start = "${day}T16:00:00+03:00"; minutes = 420 } -Token $b.token -Expect 201 | Out-Null

$cards = @(Call GET "/venues?categoryId=$($padel.id)&date=$day&minutes=60" -Expect 200)
Check ($cards.Count -eq 2) 'two published venues for the sport (unpublished one hidden)'
Check ($cards[0].id -eq $a.id -and $cards[0].times.Count -eq 8 -and $cards[0].fromTotal -eq 100) 'venue with times first, 8 times, from 100.00'
Check ($cards[1].id -eq $b.id -and $cards[1].times.Count -eq 0 -and $null -eq $cards[1].fromTotal) 'fully blocked venue last, no times'
Check ($cards[0].times[0].localTime -eq '16:00' -and $cards[0].times[0].start -like "*+03:00") 'times carry venue offset'
Call GET "/venues?categoryId=$($padel.id)&date=$day&minutes=45" -Expect 400 | Out-Null
$far = @(Call GET "/venues?categoryId=$($padel.id)&date=$([DateTime]::UtcNow.AddDays(60).ToString('yyyy-MM-dd'))&minutes=60" -Expect 200)
Check ($far.Count -eq 2 -and $far[0].times.Count -eq 0) 'a day beyond 30 days lists venues without times'

$detail = Call GET "/venues/$($a.id)" -Expect 200
Check ($detail.hours.Count -eq 7 -and $detail.categories.Count -eq 1 -and $detail.courts[0].features[0].nameEn -eq 'Panoramic') 'venue page: hours, sport, court features'
Check ($detail.freeCancellationHours -eq 24 -and $detail.currency -eq 'SAR') 'venue page: cancellation hours and currency'
Call GET "/venues/$($hidden.id)" -Expect 404 | Out-Null

$cors = Invoke-WebRequest -Uri "$api/categories" -Method OPTIONS -Headers @{ Origin = 'http://localhost:5400'; 'Access-Control-Request-Method' = 'GET' } -SkipHttpErrorCheck
Check ("$($cors.Headers['Access-Control-Allow-Origin'])" -eq 'http://localhost:5400') 'CORS allows the app web origin in development'
$evil = Invoke-WebRequest -Uri "$api/categories" -Method OPTIONS -Headers @{ Origin = 'http://evil.test'; 'Access-Control-Request-Method' = 'GET' } -SkipHttpErrorCheck
Check (-not $evil.Headers.ContainsKey('Access-Control-Allow-Origin')) 'CORS refuses other origins'

Write-Host "`nFailures: $script:failures"
```

Expected: `Failures: 0`. Then (after 60 s) the M5 script still passes (availability unchanged).

- [ ] **Step 2: App checks.** From `app/` (PowerShell): `flutter analyze` → no issues; `flutter build web --dart-define=API_BASE_URL=http://localhost:5300` → succeeds.

- [ ] **Step 3: Walkthrough.** Seed a published venue (as the M7 seed). Run `flutter run -d web-server --web-port 5400 --dart-define=API_BASE_URL=http://localhost:5300` (PowerShell, background). In Chrome via chrome-devtools at a phone size (enable Flutter semantics by clicking `flt-semantics-placeholder`): Play shows sports, days, lengths and the seeded venue with times → tap a time → confirm sheet → Book → sign-in (phone, code from the API log, name) → confirmation with reference and QR → Bookings shows it → detail → cancel (free) → it moves to Past. Switch to Arabic in Profile: RTL, Western digits, time ranges read left-to-right. Then type the reference into the M7 Desk to confirm the QR content matches.

- [ ] **Step 4:** Tick M8 in the roadmap, commit, merge.

## What M8 deliberately leaves out

- Distance / "near me", maps and directions (needs location permission and a map package).
- Filters by court features (the data is in the venue page; filter chips later).
- Push notifications and reminders (M9).
- Bundled fonts (google_fonts still downloads them on first run — bundle before release).
- Android/iOS store setup, app icons, splash.

## Verification results

- API script: `Failures: 0` (cards, order, 8 times, from-price, blocked venue, 60-day day, venue page, 404 unpublished, CORS allow/refuse). M5 re-run after the availability refactor: `Failures: 0`.
- `flutter analyze`: no issues; `flutter build web`: built.
- Browser (web-server :5400, 500 px wide): Play → time → confirm sheet → Book → phone → code (from the API log) → name → booked (reference + QR) → View my bookings → detail → cancel (dialog said late, correctly) → Past shows *Late cancellation*; Profile → Arabic: RTL, Western digits, time ranges LTR.
- Fixed during the walkthrough: chip labels were clipped because google_fonts loaded after the first layout — `main()` now awaits `GoogleFonts.pendingFonts` for every weight used (also removes the Arabic empty-box flash from M1); the booking summary showed a free-cancellation time that had already passed and a pay-at-venue line on cancelled bookings — now only live bookings show them, and a passed deadline reads *Cancelling now counts as a late cancellation.*
- Known: API error sentences are English in the Arabic UI (API messages are English-only, noted since M3b).
