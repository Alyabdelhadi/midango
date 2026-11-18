# A3 App Content (operator) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** From Business, the Midango operator controls what the app shows: a home banner, featured venues, a push message to every user, and a force-update / maintenance switch.

**Defaults (stated to the owner, no extra questions):**
- One banner at a time (on/off), EN/AR title + optional text + optional image; tapping opens a venue, a tournament, a web link, or nothing.
- Featured venues: an ordered list of published venues (max 10), shown as a strip at the top of the Play tab.
- "Message all users" sends one push to every registered device in its language and keeps a history. No audience targeting yet.
- Force update: a minimum build number per platform; an older app shows a blocking "Update Midango" screen with the store button. Maintenance: the app shows a blocking screen with the operator's message and a Try again button. The API keeps working (Business must keep working); the switch is app-side only. If the config can't be fetched, the app opens normally.
- Store links (Android / iOS) live here; A4 reuses them for the website.

## Data
- `AppContent` singleton (Id = 1, seeded by the migration): `BannerActive bool`, `BannerTitleEn/Ar` (≤ 80), `BannerBodyEn/Ar` (≤ 200)?, `BannerImagePath?`, `BannerLinkType` ("None"|"Venue"|"Tournament"|"Url"), `BannerLinkTarget?` (venue id / tournament id / https URL ≤ 500), `MinAndroidBuild int` (default 1), `MinIosBuild int` (1), `AndroidStoreUrl?`, `IosStoreUrl?` (https, ≤ 500), `Maintenance bool`, `MaintenanceMessageEn/Ar?` (≤ 300), `UpdatedAt`.
- `Venue.FeaturedRank int?` (null = not featured; 1 = first).
- `Broadcast { Id, TitleEn, TitleAr (≤ 80), BodyEn, BodyAr (≤ 300), SentAt, SentByStaffId, Devices int, Delivered int }`.

## API
- **Operator** (`Policies.PlatformOperator`), `Platform/AppContentEndpoints.cs`:
  - `GET /admin/app-content` → all fields + `bannerImageUrl` + `featured: [{ id, nameEn, nameAr, logoUrl, published }]` (by rank).
  - `PUT /admin/app-content` (everything except the image and featured). Validation: active banner needs both titles; link Venue → a published venue id; Tournament → a published tournament id; Url → absolute https; store URLs https or empty; min builds 1–100000; maintenance on needs both messages. 400 with a sentence otherwise.
  - `PUT /admin/app-content/banner-image` (multipart, `MediaStore` Photo) / `DELETE` → the GET shape. Replacing deletes the old file.
  - `PUT /admin/app-content/featured` `[venueId]` (≤ 10, distinct, each published) → sets ranks 1..n, clears the rest → GET shape.
  - `POST /admin/broadcasts` `{ titleEn, titleAr, bodyEn, bodyAr }` (titles 1–80, bodies 1–300) → sends to all `CustomerDevices` grouped by language (FCM multicast takes ≤ 500 tokens per call: chunk), prunes dead tokens like the reminder service, saves the row with `Devices` (tokens tried) and `Delivered` (tried − dead) → the row. Data payload `{ "kind": "broadcast" }`. ponytail: sent inside the request; move to a background job if device counts make it slow.
  - `GET /admin/broadcasts` → latest 50 `{ id, titleEn, titleAr, bodyEn, bodyAr, sentAt, sentBy, devices, delivered }`.
- **Public**, no auth: `GET /app/config` → `{ minAndroidBuild, minIosBuild, androidStoreUrl, iosStoreUrl, maintenance, maintenanceMessageEn, maintenanceMessageAr, banner: { titleEn, titleAr, bodyEn, bodyAr, imageUrl, linkType, linkTarget } | null, featured: [{ id, nameEn, nameAr, logoUrl, photoUrl (first photo), addressEn, addressAr, categoryIds }] }`. Banner null when off or when its venue/tournament link is no longer published; featured only published, not suspended venues.

## Business (operator)
- Nav "App content" (lucide `Smartphone`) after Customers, operator only, route `/app-content`. One page, four cards:
  1. **Home banner**: on/off, titles/text EN+AR, image upload/remove (reuse `UploadButton`), link type select + target (venue select from published venues, tournament select from published tournaments, or URL input). Save.
  2. **Featured venues**: ordered list with up/down/remove, "Add venue" select (published venues not yet featured), max 10. Save.
  3. **Message all users**: titles/texts EN+AR, Send (confirm dialog "Send to every Midango user?"), then a toast with devices reached; history table (sent, title, by, devices / delivered).
  4. **App version & maintenance**: min Android / iOS build, store links, maintenance toggle + messages EN+AR. Save (confirm when turning maintenance on).
- EN/AR, RTL. The GET shape also carries the pick lists: `venues: [{ id, nameEn, nameAr }]` (published) and `tournaments: [{ id, nameEn, nameAr, startsAt }]` (published, not ended).

## App
- Add `package_info_plus` (real build number) and `url_launcher` (store / web links).
- `app_config.dart`: fetch `/app/config` at start and on resume (`AppLifecycleListener`); a gate above `HomeShell`: maintenance → full screen (logo, message in app language, Try again); `buildNumber < min for platform` → full screen "A new version of Midango is ready" + Update button (store URL; hidden if none). Web never forces an update. Fetch failure → no gate.
- Play tab top (all modes): the banner card (image if any, title, text; tap → venue page / tournament page / external link) and the "Featured" horizontal strip (photo or logo, name, address; tap → `VenuePage` with the currently selected sport if the venue offers it, else its first category; today; that sport's first duration).
- Broadcast pushes: the existing foreground snackbar shows them; a tap just opens the app.
- EN/AR strings.

---

### Task 1: API
Entities + `MidangoDb` config, migration `AppContent` (seed the row), `Platform/AppContentEndpoints.cs`, map in `Program.cs`. `dotnet build` clean. Commit `feat(api): app content — banner, featured venues, broadcasts, force update and maintenance`.

### Task 2: Business
Page + nav + types + EN/AR. Build + lint clean. Don't commit.

### Task 3: App
As above. `flutter analyze` clean. Don't commit.

### Task 4: Verify (controller)
API: validation sentences; banner on → `/app/config` shows it; banner linked to an unpublished venue → null; image upload/replace; featured order + unpublished excluded + > 10 refused; broadcast to fake tokens → pruned, `devices`/`delivered` counts, history; manager 403. Browser: Business page in EN + AR; app web: banner + featured strip, maintenance screen, force-update screen (set min build 2).
