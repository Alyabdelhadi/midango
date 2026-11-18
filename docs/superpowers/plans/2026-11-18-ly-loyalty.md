# LY Loyalty Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Players earn Midango points for the cash they actually pay at the desk, climb tiers that earn faster, and spend points as money off an upcoming booking; Midango owes venues for the discounts.

**Decisions (user, 2026-11-18):** platform-wide points funded by Midango; points buy money off a booking; tiers that earn faster. **Defaults (stated to the user, changeable on the Rules page):** 1 point per SAR paid; tiers from checked-in games in the last 90 days — Bronze 0+ ×1, Silver 10+ ×1.25, Gold 25+ ×1.5; 100 points = SAR 10; redeem in steps of 100 points; at most 50 % of the customer's own amount; no expiry yet.

## Data
- `PlatformSettings` gains: `PointsPerSar` (decimal, default 1), `SilverGames` (10), `GoldGames` (25), `SilverMultiplier` (1.25), `GoldMultiplier` (1.5), `PointsPerRedeemStep` (100), `SarPerRedeemStep` (10), `MaxRedeemPercent` (50), `TierWindowDays` (90).
- `PointsEntry { long Id, CustomerId, int Points (+earn / −redeem / ±reversal), Kind (Earn | Redeem | EarnReversal | RedeemReleased), BookingId?, At, Note? }` — the ledger; **balance = SUM(points)**, never stored elsewhere. Index (CustomerId, At).
- `Booking` gains `PointsRedeemed` int (default 0) and `PointsDiscount` decimal (default 0) — the organiser's redemption on this booking. `MatchPlayer` gains the same two columns — a joiner's redemption on their share.
- "Amount to pay" for the organiser = organiser share (or Total) − PointsDiscount; for a player = share − their PointsDiscount.

## Rules
- **Earn** at check-in (booking check-in for the organiser / plain bookings; player check-in for joiners): `points = floor(cashCollected × PointsPerSar × tierMultiplier)`, one `Earn` entry with the BookingId. Only for bookings with a customer account (not walk-ins without one). Undo check-in → `EarnReversal` entry of the same size (negative). Cash collected = the amount actually paid (after the points discount).
- **Tier** = by count of checked-in games (bookings as organiser + match players checked in) with start in the last `TierWindowDays`; computed on read, never stored.
- **Redeem** `POST /bookings/{id}/points` `{ points }` (customer, organiser or active player of an upcoming, not-checked-in, Confirmed booking): `points` is a multiple of `PointsPerRedeemStep`, ≤ balance, and the discount `points / step × SarPerRedeemStep` ≤ `MaxRedeemPercent`% of that customer's own amount. Replaces any previous redemption on that booking (release old → `RedeemReleased` +, then `Redeem` −) in one transaction with the customer row locked (`SELECT … FOR UPDATE`) so the balance can't go negative under concurrency. `points: 0` removes it.
- **Release** automatically (`RedeemReleased`) when the booking is cancelled (by anyone), the player leaves/is removed, or the booking/player is marked no-show.
- Venue settlement: the discount is money Midango owes the venue; check-in records `CashCollected` = amount paid, and the redeemed SAR is reported separately.

## API
- `GET /me/points` → `{ balance, tier: "Bronze"|"Silver"|"Gold", gamesInWindow, nextTier: { name, gamesNeeded } | null, multiplier, redeem: { pointsPerStep, sarPerStep, maxPercent }, history: [{ at, points, kind, bookingId, venueNameEn, venueNameAr }] (latest 50) }`.
- `POST /bookings/{id}/points` `{ points }` → the customer BookingDto, which gains `pointsRedeemed`, `pointsDiscount`, `amountToPay`.
- Staff booking DTO (and each match player in it) gains `pointsDiscount` and `amountToPay`; the desk button reads "Take {amountToPay} & check in".
- `GET/PUT /admin/settings` gain the loyalty fields with validation (multipliers 1–5, games 1–500, percent 0–100, steps > 0).
- Today: "Paid with points" figure; Reports: per court/week `pointsDiscount` alongside cash ("owed by Midango").

## Admin
- Rules page: a "Loyalty" card with the settings fields and a one-line example ("A Silver player paying SAR 100 earns 125 points; 100 points = SAR 10 off").
- Desk / booking panel / players: show "SAR x with points" under the amount when a discount exists; buttons use `amountToPay`.
- Today: "Paid with points" tile; Reports: an "Owed by Midango (points)" column/total.

## App
- Profile: a points card (balance, tier badge, progress to next tier "{n} more games to Silver", earn multiplier) → "Points history" page.
- Booking detail (organiser or player, upcoming, not checked in): "Use points" — a stepper in redeem steps (0 … max allowed), shows "SAR x off · you pay SAR y at the desk", Save; removing sets 0.
- Confirmation/booking summaries show "Pay SAR y (SAR x with points)".
- Strings EN/AR.

---

### Task 1: API (+ admin Rules/desk/Today/report changes)
Entities + migration `Loyalty`, `Loyalty/LoyaltyEndpoints.cs` + `Loyalty/Points.cs` (earn/reverse/redeem/release helpers used by desk, cancel, leave, remove, no-show paths), settings, DTO fields, Today/Reports figures; admin changes. `dotnet build`, admin build/lint clean. Commit.

### Task 2: App
Profile card + history page, booking-detail "Use points", summaries. `flutter analyze` clean. Don't commit.

### Task 3: Verify (controller)
- API script: check-in earns floor(cash × rate × multiplier); undo reverses; tier thresholds via SQL-seeded check-ins; redeem validation (step, balance, 50 % cap); concurrent redeems can't overdraw; cancel/leave/no-show release; desk amountToPay and cash recorded; Today/report "with points" figures; settings validation.
- Browser: Rules loyalty card; app Profile points card + history; apply points on a booking; desk shows reduced amount and checks in; EN + AR.
