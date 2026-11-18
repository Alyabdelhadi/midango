# MMa Open Matches (server + app) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A customer can open their booking to other players; others find open matches in the app, join instantly, and pay an equal share at the desk. MMb (later) adds per-player check-in, cash and no-show at the desk.

**Decisions (user, 2026-11-18):** levels are simple bands (Beginner / Intermediate / Advanced) per sport, self-set; instant join; equal shares paid at the desk, organiser covers empty spots; players per match is set per sport by the operator (organiser may lower it); joiners follow booking rules (free leave until the venue's window, later = late; no-show per player counts toward the limit); a match can be opened at booking time or later.

## Data
- `SportCategory.PlayersPerMatch` int, 2–22, default 4 (migration sets 4 for existing rows).
- `CustomerLevel { CustomerId, CategoryId, Level }` — key (CustomerId, CategoryId); `Level` enum `Beginner | Intermediate | Advanced` stored as string.
- `Booking` gains `IsOpenMatch` bool (default false), `MaxPlayers` int? (set while open), `MatchLevel` (same enum)? — null = any level.
- `MatchPlayer { BookingId, CustomerId, JoinedAt, LeftAt?, LateLeave bool, RemovedByOrganiser bool, ReminderSentAt?, CheckedInAt?, CashCollected?, NoShowAt? }` — key (BookingId, CustomerId); a row with `LeftAt == null` is an *active* joiner. The organiser is `Booking.CustomerId` and has no row. (The check-in/cash/no-show columns exist now so MMb needs no migration; nothing writes them yet.)
- **Share** = `Math.Round(Total / MaxPlayers, 2, AwayFromZero)` shown to everyone; the organiser pays `Total − share × activeJoiners` at the desk.
- Spots taken = 1 (organiser) + active joiners; full when = `MaxPlayers`.

## Rules (server-enforced)
- **Open** (`POST /bookings` with `match`, or `POST /bookings/{id}/open`): organiser's own Confirmed booking, not started; `maxPlayers` 2..category.PlayersPerMatch and ≥ 1 + active joiners; `level` null or a band. Re-opening updates max/level.
- **Close** (`POST /bookings/{id}/close`): only when there are no active joiners → else 409 "Remove the players who joined first."
- **Join** (`POST /matches/{id}/join`): customer has a name (else 409 as in booking); match open, Confirmed, `StartsAt > now`, not the organiser, not already active, not full → 409 "This match is full." Runs in a transaction that locks the booking row (`SELECT … FOR UPDATE` via `FromSql`) so two joins can't overfill. The **no-show limit** applies: active joined matches count as upcoming bookings (same 409 sentence as `POST /bookings`). Level is *not* enforced (the app warns). Re-joining after leaving reuses the row.
- **Leave** (`POST /matches/{id}/leave`): active joiner, before start; `LateLeave = now > FreeCancellationUntil`.
- **Remove** (`DELETE /bookings/{id}/players/{customerId}`): organiser, before start; sets `LeftAt`, `RemovedByOrganiser`.
- **Organiser cancels** the booking: existing cancel endpoint; joiners stay as rows, the match is gone with the booking; they're notified.
- **No-show limit** (`POST /bookings` and join): missed = booking no-shows (existing) + `MatchPlayer.NoShowAt` in the window; upcoming = confirmed future bookings as organiser + active joined future matches.

## API
- `GET /me/levels` → `[{ categoryId, level }]`; `PUT /me/levels/{categoryId}` `{ level }` (null level → delete).
- `GET /matches?categoryId&date&level?` (public, like `/venues`; `date` = venue-local business day) → open, Confirmed, future, not-full matches at published venues: `{ id, venueId, venueNameEn, venueNameAr, venueLogoUrl, courtNameEn, courtNameAr, startsAt, endsAt, minutes, currency, share, maxPlayers, spotsLeft, level, players: [{ firstName }] }`, ordered by start. `firstName` = first word of the customer's name (organiser first); never phone numbers.
- `GET /matches/{id}` (public) → same item + `organiserFirstName`, `addressEn`, `addressAr`, `freeCancellationUntil`.
- `POST /matches/{id}/join`, `POST /matches/{id}/leave` (customer) → the match item.
- `POST /bookings/{id}/open` `{ maxPlayers, level }`, `POST /bookings/{id}/close`, `DELETE /bookings/{id}/players/{customerId}` (organiser) → the booking DTO.
- `POST /bookings` body gains optional `match: { maxPlayers, level }`.
- `BookingDto` (customer) gains `role` (`"organiser" | "player"`), `match` (`{ maxPlayers, level, share, players: [{ customerId, firstName }] }` or null), `myShare` (null unless in a match). `/me/bookings` includes matches the customer actively joined (as `role: "player"`); the app hides organiser-only actions by `role`.
- Categories DTOs (public + admin) gain `playersPerMatch`; admin category create/update validates 2..22.

## Push (through the existing `IPushSender`, in each device's language)
- Joined → organiser: "{name} joined your match" / "انضم {name} إلى مباراتك", body "{court} · {time}".
- Full → organiser and joiners: "Your match is full" / "اكتملت مباراتك".
- Removed → that player: "You were removed from a match" / "تمت إزالتك من مباراة", body venue · time.
- Organiser cancelled → active joiners: "A match you joined was cancelled" / "أُلغيت مباراة انضممت إليها".
- Data `{ bookingId }` on all (app opens the booking).
- **Reminders:** `ReminderService` also reminds active joiners (`MatchPlayer.ReminderSentAt`), same rules.
- Sending happens after the save; failures are logged, never fail the request. Message texts and the send helper live in `Notifications/MatchNotifications.cs`.

## Admin
- Category dialog: "Players per match" number field (2–22) + EN/AR strings; categories list shows it.

## App
- **Profile:** "Your level" section — for each sport (from `/categories`) a 3-option segmented control (+ "Not set"); signed-in only.
- **Play tab:** a top switch "Courts | Open matches". Open matches: same sport/date chips, a level filter (Any + bands, default = my level for that sport), list of match cards (venue + logo, court, time, level pill, "{n} spots left", share price, player first-name initials). Empty state.
- **Match page:** details, players list (initials + first names, organiser marked), share and "pay at the desk", free-leave time, **Join** (signed-out → existing sign-in flow first; level mismatch → a line "This match is for Intermediate players" with "Join anyway") / **Leave** (confirm; late warning after the window). After join, ask push permission once (M9b's `askPermissionOnce`).
- **Confirm sheet:** switch "Open to other players" → players stepper (2..playersPerMatch, default playersPerMatch) + level picker (Any default); shows "Your share" preview.
- **Booking detail:** organiser of an upcoming booking: "Open to other players" (same controls) or, when open, the players list with Remove, max/level edit, Close (disabled with reason while joiners). Player (`role: player`): match info, share, Leave.
- **Bookings tab:** joined matches appear with a "Match" pill; history shows left/removed matches as such.
- All strings EN/AR; RTL.

---

### Task 1: API + admin field
Entities, migration `OpenMatches`, endpoints above in `Bookings/MatchEndpoints.cs` (+ changes in `BookingEndpoints.cs`, `CategoryEndpoints.cs`, `ReminderService.cs`), `Notifications/MatchNotifications.cs`, admin category dialog field. `dotnet build` 0 errors/warnings; admin `npm run build`/`lint` 0 errors. Commit `feat: open matches — levels, open/join/leave, shares, match pushes`.

### Task 2: App
Everything under App. `flutter analyze` clean (PowerShell only). Don't commit.

### Task 3: Verify (controller)
- API script: open at booking + later; max > playersPerMatch → 400; join own → 409; two concurrent joins on the last spot → exactly one 200; full → 409; leave late flag; remove; close with joiners → 409; matches list filters (sport/date/level/full/started/unpublished excluded); share maths; no-show limit counts joined matches; reminders reach joiners; pushes logged/sent for join/full/remove/cancel.
- Browser (Flutter web, 390×844 without mobile flag): set level, open a match from the confirm sheet, second customer joins from Open matches, organiser sees the player and removes them; Arabic pass.
