# A3 App Content (operator) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** From Business, the Midango operator controls what the app shows: a home banner, featured venues, a push message to every user, and a force-update / maintenance switch.

**Defaults (stated to the owner, no extra questions):**
- The home banner is a **slider** (owner, 2026-11-18): up to 10 banners, each on/off, ordered, EN/AR title + optional text + optional image; tapping opens a venue, a tournament, a web link, or nothing. The app auto-advances every 5 s with dots.
- Featured venues: an ordered list of published venues (max 10), shown as a strip at the top of the Play tab.
- "Message all users" sends one push to every registered device in its language and keeps a history. No audience targeting yet.
- Force update: a minimum build number per platform; an older app shows a blocking "Update Midango" screen with the store button. Maintenance: the app shows a blocking screen with the operator's message and a Try again button. The API keeps working (Business must keep working); the switch is app-side only. If the config can't be fetched, the app opens normally.
- Store links (Android / iOS) live here; A4 reuses them for the website.

## Data
- `AppBanner { Id, TitleEn/Ar (1–80), BodyEn/Ar? (≤ 200), ImagePath?, LinkType ("None"|"Venue"|"Tournament"|"Url"), LinkTarget? (venue id / tournament id / https URL ≤ 500), Active, SortOrder, CreatedAt }`, table `app_banners`.
- `AppContent` singleton (Id = 1, seeded by the migration): `MinAndroidBuild int` (default 1), `MinIosBuild int` (1), `AndroidStoreUrl?`, `IosStoreUrl?` (https, ≤ 500), `Maintenance bool`, `MaintenanceMessageEn/Ar?` (≤ 300), `UpdatedAt`.
- `Venue.FeaturedRank int?` (null = not featured; 1 = first).
- `Broadcast { Id, TitleEn, TitleAr (≤ 80), BodyEn, BodyAr (≤ 300), SentAt, SentByStaffId, Devices int, Delivered int }`.

## API
- **Operator** (`Policies.PlatformOperator`), `Platform/AppContentEndpoints.cs`:
  - `GET /admin/app-content` → all fields + `banners: [{ id, titleEn, titleAr, bodyEn, bodyAr, imageUrl, linkType, linkTarget, active, sortOrder }]` + `featured: [{ id, nameEn, nameAr, logoUrl, published }]` (by rank).
  - Banners: `POST /admin/app-content/banners`, `PUT|DELETE …/banners/{id}`, `PUT …/banners/order [id]`, `PUT|DELETE …/banners/{id}/image` (MediaStore Photo) → GET shape. Max 10.
  - `PUT /admin/app-content` (versions, store links, maintenance). Validation: banner link Venue → a published venue id; Tournament → a published tournament id; Url → absolute https; store URLs https or empty; min builds 1–100000; maintenance on needs both messages. 400 with a sentence otherwise.
  - `PUT /admin/app-content/featured` `[venueId]` (≤ 10, distinct, each published) → sets ranks 1..n, clears the rest → GET shape.
  - `POST /admin/broadcasts` `{ titleEn, titleAr, bodyEn, bodyAr }` (titles 1–80, bodies 1–300) → sends to all `CustomerDevices` grouped by language (FCM multicast takes ≤ 500 tokens per call: chunk), prunes dead tokens like the reminder service, saves the row with `Devices` (tokens tried) and `Delivered` (tried − dead) → the row. Data payload `{ "kind": "broadcast" }`. ponytail: sent inside the request; move to a background job if device counts make it slow.
  - `GET /admin/broadcasts` → latest 50 `{ id, titleEn, titleAr, bodyEn, bodyAr, sentAt, sentBy, devices, delivered }`.
- **Public**, no auth: `GET /app/config` → `{ minAndroidBuild, minIosBuild, androidStoreUrl, iosStoreUrl, maintenance, maintenanceMessageEn, maintenanceMessageAr, banners: [{ id, titleEn, titleAr, bodyEn, bodyAr, imageUrl, linkType, linkTarget }], featured: [{ id, nameEn, nameAr, logoUrl, photoUrl (first photo), addressEn, addressAr, categoryIds }] }`. Banners: active, by order, skipping any whose venue/tournament link is no longer published; featured only published, not suspended venues.

## Business (operator)
- Nav "App content" (lucide `Smartphone`) after Customers, operator only, route `/app-content`. One page, four cards:
  1. **Home banner**: slider list: each banner row with image thumb, title, on/off, up/down, edit, delete; Add banner (max 10). Edit dialog: titles/text EN+AR, image upload/remove (reuse `UploadButton`), link type select + target (venue select from published venues, tournament select from published tournaments, or URL input). Save.
  2. **Featured venues**: ordered list with up/down/remove, "Add venue" select (published venues not yet featured), max 10. Save.
  3. **Message all users**: titles/texts EN+AR, Send (confirm dialog "Send to every Midango user?"), then a toast with devices reached; history table (sent, title, by, devices / delivered).
  4. **App version & maintenance**: min Android / iOS build, store links, maintenance toggle + messages EN+AR. Save (confirm when turning maintenance on).
- EN/AR, RTL. The GET shape also carries the pick lists: `venues: [{ id, nameEn, nameAr }]` (published) and `tournaments: [{ id, nameEn, nameAr, startsAt }]` (published, not ended).

## App
- Add `package_info_plus` (real build number) and `url_launcher` (store / web links).
- `app_config.dart`: fetch `/app/config` at start and on resume (`AppLifecycleListener`); a gate above `HomeShell`: maintenance → full screen (logo, message in app language, Try again); `buildNumber < min for platform` → full screen "A new version of Midango is ready" + Update button (store URL; hidden if none). Web never forces an update. Fetch failure → no gate.
- Play tab top (all modes): the banner slider (`PageView`, auto-advance 5 s, pauses while dragged, dots; each slide: image if any, title, text; tap → venue page / tournament page / external link) and the "Featured" horizontal strip (photo or logo, name, address; tap → `VenuePage` with the currently selected sport if the venue offers it, else its first category; today; that sport's first duration).
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
API: validation sentences; banners add/order/off → `/app/config` follows; a banner linked to an unpublished venue is skipped; 11th banner refused; image upload/replace; featured order + unpublished excluded + > 10 refused; broadcast to fake tokens → pruned, `devices`/`delivered` counts, history; manager 403. Browser: Business page in EN + AR; app web: banner + featured strip, maintenance screen, force-update screen (set min build 2).

---

## Verification (2026-11-18)

**Change during the build:** the owner asked for the banner to be a slider, so a single banner became up to 10 ordered banners (`app_banners`).

**API** (`a3-check.ps1`): **0 failures.** The first run had 2 misses, both my test's fault: device registration returns 204, not 200.
- **Access:** a venue manager gets 403 on both the content page and broadcasts.
- **Banner validation:** missing Arabic title, `http://` link, unpublished venue and unknown link type each get 400 with a sentence. An 11th banner gets 400 "You can have up to 10 banners."
- **Banners:** three banners are saved. Replacing an image deletes the old file, and a fake PNG gets 400. `/app/config` returns only the active banners, in order. A partial reorder gets 400 "Send every banner once"; a full reorder and turning a banner on both show up in `/app/config`. Unpublishing the linked venue drops that banner.
- **Featured:** a repeat, an unpublished venue and 11 venues each get 400. The order holds, with `categoryIds`, and a suspended venue is left out.
- **Version and maintenance:** build 0, a non-https store link and maintenance with only the English message each get 400. Maintenance and min build 2 reach `/app/config`, and the rest of the API keeps serving.
- **Broadcast:** a missing Arabic title gets 400. The broadcast tried every registered device. They were all fake, so 0 were delivered and every fake token was pruned. It is listed first in the history with "Midango Operator".

**Browser, Business:** the App content page shows the banner rows (thumbnail, pill, link summary), featured venues, the message history (delivered / devices) and the version and maintenance form. In Arabic it is RTL, and the edit dialog opens with the saved image, the venue selected and the upload control.

**Browser, app (web, 390×844):**
- **Play tab:** the slider sits on top, with dots, and advances every 5 s (Padel night → Read more → Riyadh Open). The Featured strip shows Smash Courts and Smash Padel Club.
- **Taps:** tapping a featured card opens that venue page. Tapping the venue-linked banner opens the venue in Arabic.
- **Maintenance:** turning it on shows the logo, the title, the operator's message and "Try again". Turning it off and pressing Try again brings the Play tab back.
- **Arabic:** the slider, the strip, the dots and the tabs are mirrored correctly.

**Not walked:**
- The force-update screen: by design the web app never forces an update. It's queued for the on-phone pass, with min Android build set to 2.
- A real broadcast reaching a phone, queued with the push test.
- External links (`url_launcher`) opening on Android.

**Known limits:** broadcasts send inside the request (marked `ponytail:`). There is no audience targeting yet. A featured venue with no photo or logo shows a grey block.
