# A2 Customers (operator) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The platform operator can find any app customer, see their activity across all venues, block/unblock the account, and lift a no-show limit.

**Decisions (user-approved scope "Customers: search all app customers across venues, see their bookings and no-shows, block/unblock an account, lift a no-show limit"; defaults stated to the user):** the operator sees full phone numbers (Midango support); blocking needs a reason, signs the customer out everywhere, and stops sign-in, booking, joining matches, registering tournament teams and redeeming points; lifting the limit makes earlier no-shows stop counting (history kept).

## Data
- `Customer` gains `BlockedAt?`, `BlockedReason` (≤ 300)?, `BlockedByStaffId?`, `NoShowsClearedAt?` (no-shows at or before this instant don't count toward the limit).

## Rules
- **Block** `POST /admin/customers/{id}/block` `{ reason }` (operator; reason 3–300 chars): sets the three fields, revokes all the customer's refresh tokens. Repeat = no-op returning the detail.
- **Unblock** `POST /admin/customers/{id}/unblock`: clears them.
- **Lift limit** `POST /admin/customers/{id}/lift-no-show-limit`: `NoShowsClearedAt = now`.
- **Enforcement** (409 "This account is blocked. Contact Midango." unless noted):
  - `POST /auth/otp/request` for a blocked phone → 403 with that sentence (no code sent); `POST /auth/otp/verify` → 403.
  - `/auth/refresh` for a blocked customer → 401 (existing path).
  - Write paths check via one helper `Customers.BlockedAsync(db, customerId, ct)`: `POST /bookings`, `POST /matches/{id}/join`, `POST /tournaments/{id}/teams`, `POST /bookings/{id}/points`. (Existing access tokens live ≤ 15 min; reads stay allowed.)
- **No-show limit** (`BookingEndpoints.NoShowLimitAsync`): count only no-shows with `NoShowAt > max(window start, NoShowsClearedAt)`.

## API (operator, `Policies.PlatformOperator`)
- `GET /admin/customers?q=&page=` → `{ items: [{ id, name, phone, createdAt, bookings, noShowsInWindow, limited, blocked }], total }`, 50 per page, newest first; `q` matches name (case-insensitive contains) or phone digits (contains, digits only, ≥ 3 digits; local `05…` also matched against `+9665…`).
- `GET /admin/customers/{id}` → `{ id, name, phone, createdAt, blockedAt, blockedReason, blockedBy, noShowsClearedAt, stats: { bookings, checkedIn, cancelled, lateCancellations, noShows, noShowsInWindow, matchesJoined, tournamentTeams }, limited, points: { balance, tier }, devices, bookings: [latest 50: { id, reference, venueNameEn, venueNameAr, courtNameEn, courtNameAr, startsAt, minutes, total, status, role ("organiser"|"player"), checkedInAt, noShowAt, lateCancellation, cancelledByVenue }], tournaments: [{ tournamentId, nameEn, nameAr, teamName, status, startsAt }] }`. Times in the booking's venue offset. `limited` = would `NoShowLimitAsync` limit them right now.
- Block/unblock/lift return the detail.

## Admin (Business, operator)
- Nav "Customers" (lucide `UserSearch`) after Venues — operator only (the venue Customers page stays as is for venue staff; use a distinct route like `/all-customers`).
- List: search box, table (name, phone in `<bdi dir="ltr">`, joined, bookings, no-shows (window), status pills Blocked / Limited), pagination.
- Detail: header with name/phone/joined, status (Blocked with reason + by + when, Limited), stats tiles, points + tier, bookings table (venue, court, time, status pill, role, late/venue-cancelled marks), tournaments list; actions: **Block** (dialog with reason), **Unblock**, **Lift no-show limit** (confirm; shown when limited or any no-shows in window). EN/AR, RTL.

## App
- No new screens; the existing error display already shows `title` on sign-in and booking.

---

### Task 1: API
`Identity/Entities.cs` fields, migration `CustomerControls`, `Identity/AdminCustomerEndpoints.cs` (list/detail/actions), `Identity/Customers.cs` (BlockedAsync helper), enforcement edits in `CustomerEndpoints.cs` (OTP), `TokenEndpoints.cs` (refresh), `BookingEndpoints.cs` (book + NoShowLimitAsync), `MatchEndpoints.cs` (join), `Tournaments/TournamentEndpoints.cs` (register), `Loyalty/LoyaltyEndpoints.cs` (redeem). `dotnet build` clean. Commit `feat(api): operator customers — search, activity, block, lift no-show limit`.

### Task 2: Business
Pages above. Build + lint clean. Don't commit.

### Task 3: Verify (controller)
API: search by name / partial phone / local 05 form; detail stats; block → OTP request 403, verify 403, refresh 401, book/join/register/points 409, reads fine; unblock restores; lift limit lets a limited customer book again; operator-only (manager 403). Browser: list → detail → block/unblock/lift in EN + AR.
