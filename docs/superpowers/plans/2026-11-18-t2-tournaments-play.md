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

---

## Verification (2026-11-18)

Teams were inserted into `tournament_teams` with SQL so draws of any size could be tested without the sign-in rate limit.

**API** (`t2-check.ps1`): **0 failures on the first run.**
- **Drawing:** a draw before registration closes gets 409.
- **Knockout shapes:** n=2 gives 1 match / 0 byes / 1 round. n=3 gives 3 / 1 / 2. n=5 gives 7 / 3 / 3. n=8 gives 7 / 0 / 3. A team with a bye already sits in round 2.
- **Schedule (8 teams):** no court is booked twice, and round 2 starts at 08:00 after round 1's last match at 07:00 plus 60 minutes.
- **Results:** a knockout tie gets 400 "needs a winner". Once a result is in, a redraw gets 409. Playing on gives Finished with a champion. Changing a round-1 result after the next match has one gets 409. Clearing the final reopens the tournament (InProgress, champion cleared). The public detail carries the bracket.
- **Round robin:**
  - With 5 teams there are 10 matches and every pair meets once. One team winning all and every other match drawn 2–2 gives standings of 12 points (4 wins) then 3 each, and the group winner is the champion.
  - With 8 teams there are 2 groups of 4 and 12 matches, and no team is scheduled twice at once.
- **Other rules:**
  - A 2-hour window with 90-minute matches gives `overrunsEnd`.
  - The draw push reached the captain (fake token pruned).
  - Rescheduling onto another match's court and time gets 409 "Another match of this tournament is on that court then".
  - Withdrawing a team clears the draw back to Published, and the redraw with 7 teams has 1 bye.

**Browser:** the Business detail for a finished round-robin shows "Champion: Team 1", the Tables (12 / 3 / 3 / 3 / 3 with For/Against) and the Fixtures with times, courts, "(winner)" marks and score entry.

**Fix during review:** the format help said round-robin groups' "best of each group go through", but there's no knockout stage after the groups. Business and the app now say "the table decides the winner", in English and Arabic.

**Not walked:** the app's Draw and Fixtures tabs (analyze is clean). They're queued for the final on-phone pass.

**Known limits:** the scheduler ignores opening hours on multi-day events (marked `ponytail:`). Round robin has no knockout stage after the groups. Groups are numbered, not lettered.
