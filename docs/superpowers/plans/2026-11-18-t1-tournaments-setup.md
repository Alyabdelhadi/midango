# T1 Tournaments — Set-up & Sign-up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A venue creates a tournament in Business and publishes it (its courts get blocked); players find it in the app and a captain registers a team (waitlist when full); on the day the desk checks teams in and takes the entry fee. T2 (later) adds the draw, fixtures, results, bracket/standings.

**Decisions (user, 2026-11-18):** venues create tournaments in Business (operator via Open venue); formats Knockout and Round-robin groups (format chosen now, used by T2); team sign-up in the app by a captain, fee per team paid at the venue; courts blocked automatically.

## Data
- `Tournament { Id, VenueId, CategoryId, NameEn, NameAr, DescriptionEn?, DescriptionAr?, Format ("Knockout"|"RoundRobin"), TeamSize int, MaxTeams int, EntryFee decimal, Currency, StartsAt, EndsAt, RegistrationClosesAt (UTC), CourtIds uuid[], Level (PlayLevel?, null = any), Status ("Draft"|"Published"|"Cancelled"), CreatedAt, PublishedAt?, CancelledAt? }`.
- `Booking` gains `TournamentId` Guid? (set on the blocks a tournament creates).
- `TournamentTeam { Id, TournamentId, Name (≤ 40), CaptainCustomerId, Status ("Registered"|"Waitlisted"|"Withdrawn"), RegisteredAt, WithdrawnAt?, CheckedInAt?, CashCollected? }`; team names unique (case-insensitive) per tournament among non-withdrawn teams — enforce in code inside the locked transaction.
- `TournamentTeamMember { TeamId, Position, Name (≤ 60), CustomerId? }` — position 0 = captain (CustomerId set, Name = account name); others are names typed by the captain.
- Default `TeamSize` when creating = `max(1, category.PlayersPerMatch / 2)` (padel 4 → 2, football 10 → 5); editable 1–11.

## Rules
- **Create / edit (Draft):** manager of the venue. Validation: names EN/AR 1–120; description ≤ 2000; category active; courts belong to the venue, active, of that category, 1–20 of them; `StartsAt` in the future, `EndsAt` > start, ≤ 3 days long; `RegistrationClosesAt` ≤ `StartsAt`; MaxTeams 2–64; TeamSize 1–11; EntryFee 0–10 000.
- **Publish:** Draft → Published. In one transaction create one `Block` booking per court for [StartsAt, EndsAt) with `TournamentId`, `Note = "Tournament: {NameEn}"`, total 0. If the exclusion constraint fires, roll back and return 409 with title "These courts are already booked during the tournament." and an extension `clashes: [{ courtNameEn, courtNameAr, startsAt, endsAt, reference }]` (query the overlapping confirmed bookings to build the list).
- **Edit after publish:** only names/descriptions, `MaxTeams` (≥ registered teams; raising it promotes waitlisted teams in order), `RegistrationClosesAt` (still ≤ start). Times, courts, format, team size, fee are locked once published (400 "Cancel and create a new tournament to change this.").
- **Cancel:** Draft or Published → Cancelled; its blocks become Cancelled (court free again); all Registered and Waitlisted captains get a push "{tournament} was cancelled" / "أُلغيت بطولة {name}".
- **Register** (customer, `POST /tournaments/{id}/teams`): tournament Published, `now < RegistrationClosesAt`; customer has a name; not already captain of a non-withdrawn team in it; team name unique; `members` = exactly TeamSize − 1 names (trimmed, 1–60 chars). Lock the tournament row (`SELECT … FOR UPDATE`) and count Registered teams: `< MaxTeams` → Registered, else Waitlisted. Response tells which.
- **Withdraw** (captain): until `StartsAt`; a Registered team withdrawing promotes the oldest Waitlisted team (same lock) and pushes its captain "You're in: {tournament}" / "تم قبول فريقك في {name}". Checked-in teams can't withdraw.
- **Desk:** team check-in from 60 min before `StartsAt` until `EndsAt`, Registered teams only, `CashCollected = EntryFee`; undo within 15 min (same sentences as booking check-in).
- Loyalty: entry fees don't earn points in T1 (note in code).

## API
- Venue staff (`Policies.VenueStaff`, venue = `StaffVenueId()`), manager-only marked (M):
  - `GET /venue/tournaments` → list `{ id, nameEn, nameAr, categoryId, format, status, startsAt, endsAt, registrationClosesAt, maxTeams, registered, waitlisted, entryFee, currency }` newest first.
  - (M) `POST /venue/tournaments`, (M) `PUT /venue/tournaments/{id}` → detail.
  - `GET /venue/tournaments/{id}` → detail: all fields + `courts: [{ id, nameEn, nameAr }]` + `teams: [{ id, name, status, registeredAt, captain: { name, maskedPhone }, members: [names], checkedInAt, cashCollected }]` (Registered first by time, then Waitlisted by time, then Withdrawn).
  - (M) `POST /venue/tournaments/{id}/publish`, (M) `POST /venue/tournaments/{id}/cancel` → detail.
  - `POST /venue/tournaments/{id}/teams/{teamId}/check-in`, `/undo-check-in` → detail.
- Public: `GET /tournaments?categoryId=` → published, not ended, at published venues, soonest first: `{ id, venueId, venueNameEn, venueNameAr, venueLogoUrl, categoryId, nameEn, nameAr, format, teamSize, startsAt, endsAt, registrationClosesAt, entryFee, currency, maxTeams, registered, waitlisted, level, registrationOpen }`. `GET /tournaments/{id}` → same + `descriptionEn/Ar`, `addressEn/Ar`, `courts` (names), `teams: [{ name, members: [first names] }]` (Registered only).
- Customer: `POST /tournaments/{id}/teams` `{ name, members: [string] }` → `{ team: { id, name, status, members }, tournament: <public detail> }`; `POST /tournaments/{id}/teams/{teamId}/withdraw` → same; `GET /me/tournaments` → `[{ tournament: <public list item>, team: { id, name, status, members, checkedInAt } }]` upcoming first.
- Times in DTOs carry the venue's offset (like bookings).

## Business (admin)
- Nav "Tournaments" (lucide `Trophy`) for VenueManager and DeskStaff (desk sees list + detail + check-in only).
- List page: table with status pill (Draft/Published/Cancelled), dates, teams `registered/max` (+ waitlist), "New tournament" (manager).
- Form (dialog or page): sport, names EN/AR, descriptions EN/AR, format (Knockout / Round-robin groups) with one-line explanations, team size (default from sport), max teams, entry fee, level (any/band), courts (checkboxes of the sport's courts), start/end (`datetime-local` in venue time), registration closes. Locked fields shown read-only after publish with the reason.
- Detail page: summary + Publish (manager; on 409 show the clash list), Cancel (confirm dialog), teams table grouped Registered / Waitlist / Withdrawn: team name, captain (name + masked phone), members, status; on the day **Take {fee} & check in** / Undo per team (same patterns as the Desk).
- Desk page: if a published tournament starts today, a line "Tournament today: {name} — {checked-in}/{registered} teams checked in" linking to its detail page.
- Schedule shows the tournament blocks as blocks (already works via `Kind=Block`; show the note).
- EN/AR strings; RTL.

## App
- Play tab segment switch becomes "Courts | Open matches | Tournaments". Tournaments: sport chips, list of cards (venue + logo, name, dates, format, team size, fee per team, "{registered}/{max} teams", "Registration closes …" or "Registration closed"), empty state.
- Tournament page: details (description, venue, address, dates, courts, format explanation, team size, fee "per team, paid at the venue"), teams list (names), and:
  - not registered + open → **Register a team** (sign-in first if needed): team name + TeamSize − 1 teammate name fields (captain is "You"), submit → shows Registered or "You're on the waitlist (position n)".
  - registered/waitlisted → status card + **Withdraw** (confirm).
- Bookings tab: a "Tournaments" section listing `/me/tournaments` with status pills (Registered / Waitlist / Checked in).
- Push: tapping tournament pushes (data `{ tournamentId }`) opens the tournament page (extend `push.dart`'s tap handling).
- Strings EN/AR; RTL.

---

### Task 1: API
`Tournaments/Tournament.cs` (entities), `Tournaments/TournamentEndpoints.cs` (venue + public + customer), `Notifications/TournamentNotifications.cs`, Booking.TournamentId, DbContext config (uuid[] column for CourtIds), migration `Tournaments`. `dotnet build` clean. Commit `feat(api): tournaments — create, publish with court blocks, team sign-up, waitlist, desk check-in`.

### Task 2: Business
As above. Build + lint clean. Don't commit.

### Task 3: App
As above. `flutter analyze` clean. Don't commit.

### Task 4: Verify (controller)
- API: create validation; publish creates blocks; publish over an existing booking → 409 with clashes; cancel frees courts + pushes; edit-after-publish locks; registration (open/closed, size, duplicate name, duplicate captain, waitlist when full, concurrent last-spot registrations → one Registered one Waitlisted); withdraw promotes + pushes; raising MaxTeams promotes; desk check-in window + fee + undo; public list/detail; `/me/tournaments`.
- Browser: Business create → publish → see blocks on the Schedule; app list → register → waitlist; desk check-in; EN + AR.
