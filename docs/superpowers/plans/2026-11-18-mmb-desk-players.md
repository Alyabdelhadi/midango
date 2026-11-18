# MMb Open Matches at the Desk Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The desk sees every player of an open match and takes each player's share in cash, checks each in, or marks each a no-show — and the numbers (Today, reports) add up.

**Context (MMa, merged):** `Booking.IsOpenMatch`, `MaxPlayers`, `MatchLevel`; `MatchPlayer { BookingId, CustomerId, JoinedAt, LeftAt?, LateLeave, RemovedByOrganiser, ReminderSentAt?, CheckedInAt?, CashCollected?, NoShowAt? }` (key BookingId+CustomerId; active = `LeftAt == null`; columns for check-in/cash/no-show exist, nothing writes them yet). Share = `round(Total / MaxPlayers, 2)`; organiser pays `Total − share × activeJoiners`. Desk rules today (`Bookings/DeskEndpoints.cs`): check-in window 60 min before start → 6 h after end, takes `CashCollected = Total`; undo within 15 min; no-show allowed after start, undo within 15 min.

## Rules
- **Organiser check-in** (existing `POST /venue/desk/{id}/check-in`): for an open match, `CashCollected = organiser share` (`Total − share × activeJoiners`) instead of `Total`. Non-match bookings unchanged.
- **Player check-in** `POST /venue/desk/{id}/players/{customerId}/check-in`: active player, same window as the booking, booking Confirmed; sets `CheckedInAt`, `CashCollected = share`. 409 if already in. (No staff column on MatchPlayer — don't add one.)
- **Player undo** `…/undo-check-in` within 15 min.
- **Player no-show** `…/no-show`: after the booking start, not checked in → `NoShowAt = now`; **undo** `…/undo-no-show` within 15 min. A player no-show does not change the booking's status (the court was used). It counts toward that customer's no-show limit (MMa already counts `MatchPlayer.NoShowAt`).
- Staff **cannot** remove players (the organiser's choice). Cancelling the booking from the staff side stays as today **plus** it now notifies active joiners with the existing "A match you joined was cancelled" push (reuse `MatchNotifications`).
- Same error-sentence style as the existing desk endpoints.

## Staff DTOs
- Every staff booking DTO used by the Desk / Schedule panel / Today now-and-next gains `match`: `null` or `{ maxPlayers, level, share, organiserShare, players: [{ customerId, name, maskedPhone, joinedAt, checkedInAt, cashCollected, noShowAt }] }` — active players only, in join order. **Privacy:** the name is the customer's account name (they joined through the app at this venue, which satisfies the M6a rule); phone masked like everywhere else (`•••• 1234`). Times in venue-local offset.
- Desk lookup by reference returns the same.

## Numbers
- **Today** (`VenueInsightEndpoints` today): cash collected = bookings' `CashCollected` + match players' `CashCollected` for the day's bookings; expected cash unchanged (`Total` per booking already equals organiser share + shares).
- **Reports** (`ReportEndpoints`): cash by court and week includes match players' cash (attributed to the booking's court and start). No-show rate stays booking-level (say so in a code comment).

## Admin (Business)
- `components/DeskActions.tsx` / Desk page / `pages/schedule/BookingPanel.tsx`: for an open match show a "Players" block under the organiser line: each player row = name, masked phone (`<bdi dir="ltr">`), status pill (Waiting / Checked in · SAR x / No-show), and the same action pattern as the booking: **Take {share} & check in**, **No-show** (only after start), **Undo** (within 15 min, via the existing undo-toast pattern). The organiser's primary button reads **Take {organiserShare} & check in** for a match. A summary line "Collected SAR x of SAR y" for the match.
- EN/AR strings; RTL.

---

### Task 1: API
Changes in `Bookings/DeskEndpoints.cs`, `Bookings/StaffBookingEndpoints.cs` (DTO + staff cancel push), `Bookings/VenueInsightEndpoints.cs`, `Bookings/ReportEndpoints.cs`, maybe a small shared helper for the match DTO. No migration expected. `dotnet build` 0 errors/warnings. Commit `feat(api): desk check-in, cash and no-show per match player`.

### Task 2: Admin
Desk + Schedule panel + any Today list that shows bookings. `npm run build` + `lint` 0 errors. Don't commit.

### Task 3: Verify (controller)
- API script: match with 3 joiners; organiser check-in takes organiser share; each player check-in takes share; double check-in 409; undo within window; no-show before start 409, after start ok, undo; Today cash = sum; report cash includes players; staff cancel of an open match → joiner pushes attempted (fake-token pruning trick).
- Browser: Desk lookup by reference shows players; take cash per player in EN and AR; Schedule panel shows the same.
