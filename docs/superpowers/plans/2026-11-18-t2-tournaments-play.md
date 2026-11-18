# T2 Tournaments — Draw, Fixtures & Results Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After registration closes, the venue makes the draw (knockout bracket or round-robin groups) with an automatic court/time schedule, enters results on the day, and everyone sees the bracket, group tables and fixtures in Business and the app.

**Context (T1, merged):** `Tournament { …, Format (Knockout|RoundRobin), TeamSize, MaxTeams, StartsAt, EndsAt, RegistrationClosesAt, CourtIds, Status (Draft|Published|Cancelled) }`, `TournamentTeam { …, Status (Registered|Waitlisted|Withdrawn), CheckedInAt }`, venue/public/customer endpoints in `Tournaments/TournamentEndpoints.cs`, pushes in `Notifications/TournamentNotifications.cs`, court blocks with `Booking.TournamentId`.

## Defaults (no extra questions; stated in the summary)
- Draw order is random (shuffled server-side with `RandomNumberGenerator`). Redraw is allowed until the first result is entered.
- Teams in the draw = **Registered** teams at draw time. The venue can **withdraw a team** (staff action, e.g. a team that didn't show) while no results exist; doing so after a draw clears the draw. No waitlist promotion after registration closes.
- **Knockout:** bracket size = next power of two ≥ n (n ≥ 2); byes go to the first teams in the drawn order and advance automatically; standard seeding positions (1 v N, …) over the drawn order. Ties not allowed.
- **Round robin:** the venue picks the number of groups G (1 … floor(n/2), default 1 if n ≤ 6 else ceil(n/4)); teams dealt into groups snake-wise in drawn order; fixtures by the circle method. Standings: points (win 3, draw 1, loss 0), then score difference, then score for, then head-to-head result, then name. Draws allowed. No knockout stage after groups (later).
- **Scores** are two integers 0–99 (games/goals/points — generic).
- **Schedule:** `MatchMinutes` chosen at draw (default 60, 15–240). Matches are placed round by round onto the tournament's courts starting at `StartsAt`, each court taking the next free slot; a knockout round starts only after the previous round's last slot. If the schedule runs past `EndsAt`, the draw still succeeds and the detail carries `overrunsEnd: true` (Business shows a warning). Each match has court + time, editable by the venue afterwards (409 if it overlaps another match of this tournament on the same court).
- **Status:** drawing moves Published → `InProgress`; entering the last result (final, or last group match) → `Finished` with `ChampionTeamId` (knockout; round robin only when G = 1: the group winner).
- **Pushes:** draw published → every drawn team's captain: "The draw is out: {opponent} at {time} on {court}" / Arabic (or "You have a bye in round 1"); champion → all drawn captains "{team} won {tournament}".

## Data
- `Tournament` gains `MatchMinutes` int?, `GroupCount` int?, `DrawnAt`?, `ChampionTeamId` Guid?; `Status` gains `InProgress`, `Finished`.
- `TournamentMatch { Id, TournamentId, Stage ("Knockout"|"Group"), Round int (1-based), Position int, GroupNo int?, TeamAId?, TeamBId?, ScoreA?, ScoreB?, WinnerTeamId?, CourtId?, StartsAt?, NextMatchId?, NextSlot ("A"|"B")?, IsBye bool, ResultAt? }`.
- `TournamentTeam` gains `DrawPosition int?`, `GroupNo int?`.

## Rules
- **Draw** (manager) `POST /venue/tournaments/{id}/draw` `{ matchMinutes, groupCount? }`: status Published, or InProgress with no results; `now ≥ RegistrationClosesAt` (else 409 "Registration is still open."); ≥ 2 Registered teams. Replaces any previous draw in one transaction (lock the tournament row).
- **Withdraw team** (staff) `POST /venue/tournaments/{id}/teams/{teamId}/withdraw`: allowed while no results exist; clears an existing draw (status back to Published).
- **Result** (staff) `PUT /venue/tournaments/{id}/matches/{matchId}/result` `{ scoreA, scoreB }`: both teams known, not a bye; knockout ties → 400; sets winner and fills the next match slot; changing a knockout result → 409 once the next match has a result; `DELETE …/result` under the same condition (empties the next slot). Round-robin results can be edited/cleared any time; clearing one in a Finished tournament re-opens it (InProgress, champion cleared).
- **Reschedule** (staff) `PUT /venue/tournaments/{id}/matches/{matchId}/schedule` `{ courtId, startsAt }`: court must be one of the tournament's; within [StartsAt, EndsAt]; no other match of this tournament on that court overlapping `[startsAt, startsAt + MatchMinutes)`.
- Cancel (T1) works in any status except Finished.

## API
- Staff: the above + `GET /venue/tournaments/{id}` detail gains `matchMinutes, groupCount, drawnAt, championTeamId, overrunsEnd, matches: [match], groups: [group]`; teams gain `drawPosition, groupNo`.
- Public: `GET /tournaments/{id}` gains the same `matches`, `groups`, `championTeamName`.
- `match` JSON: `{ id, stage, round, position, groupNo, teamA: { id, name } | null, teamB: …, scoreA, scoreB, winnerTeamId, isBye, courtId, courtNameEn, courtNameAr, startsAt, nextMatchId, nextSlot }` (times in venue offset).
- `group` JSON: `{ groupNo, standings: [{ teamId, name, played, won, drawn, lost, scoreFor, scoreAgainst, points }] }`.
- `/me/tournaments` team gains `nextMatch: match | null`.

## Business
- Tournament detail gets a **Draw** card (manager): match length, groups (round robin), Draw / Redraw (confirm when a draw exists), overrun warning. Staff "Withdraw" button on Registered teams while no results exist.
- **Fixtures** list grouped by round (or group), each row: time, court, Team A vs Team B, score inputs + Save / Clear, bye label; Reschedule (court + time). Winner highlighted.
- **Bracket** view (knockout): columns per round, matches stacked, simple CSS connectors; **Tables** (round robin): one table per group. Champion banner when Finished.
- Desk "Tournament today" line also shows "Next: {time} {court} {A} v {B}".
- EN/AR, RTL (bracket mirrors).

## App
- Tournament page gains tabs **Info | Draw | Fixtures** once drawn: bracket (horizontally scrollable, rounds as columns, mirrors in RTL, my team highlighted) or group tables; fixtures list with times/courts/scores, my team's matches first ("Your next match: …").
- Bookings tab tournament rows show "Next match {time} on {court}".
- EN/AR.

---

### Task 1: API
`Tournaments/TournamentDraw.cs` (pure functions: knockout bracket with byes, round-robin circle fixtures, scheduler, standings), endpoint changes in `TournamentEndpoints.cs`, pushes, migration `TournamentPlay`. Keep the draw/schedule/standings maths in pure static functions over plain lists. `dotnet build` clean. Commit `feat(api): tournament draw, schedule, results, standings`.

### Task 2: Business
Draw card, withdraw button, fixtures with score entry + reschedule, bracket + tables, champion banner, Desk next-match line. Build + lint clean. Don't commit.

### Task 3: App
Tabs, bracket/tables, fixtures, my next match, Bookings next-match line. `flutter analyze` clean. Don't commit.

### Task 4: Verify (controller)
- API: knockout draws for n = 2, 3, 5, 8 (byes, rounds, auto-advance); round robin n = 5 G = 1 and n = 8 G = 2 (every pair once per group); schedule respects courts and rounds; overrun flag; results advance winners; knockout tie refused; edit refused after the next result; clearing re-opens; standings order; Finished + champion; redraw refused after a result; reschedule clash 409; pushes on draw/champion.
- Browser: Business draw → fixtures → scores → bracket → champion; app Draw/Fixtures tabs (EN + AR) if the automation cooperates.
