using System.Numerics;

namespace Midango.Api.Tournaments;

/// The draw, schedule and standings maths. Pure: plain lists in, plain lists out; the endpoints persist.
public static class TournamentDraw
{
    /// One planned fixture. Next is an index into the same list (knockout only), NextSlot "A" or "B".
    public sealed record PlannedMatch(
        int Round, int Position, int? GroupNo, Guid? TeamA, Guid? TeamB, bool IsBye, int? Next, string? NextSlot);

    // ---------- knockout ----------

    /// Bracket slots in standard seeding order, so seed 1 and 2 can only meet in the final:
    /// 2 → [1,2], 4 → [1,4,2,3], 8 → [1,8,4,5,2,7,3,6]. Each doubling pairs seed s with (2·count + 1 − s).
    public static int[] SeedOrder(int size)
    {
        var order = new List<int> { 1 };
        while (order.Count < size)
        {
            var sum = order.Count * 2 + 1;
            order = order.SelectMany(s => new[] { s, sum - s }).ToList();
        }
        return order.ToArray();
    }

    /// A single-elimination bracket over the drawn order (index 0 = seed 1). The bracket is the next power of
    /// two ≥ n; the missing seeds n+1… are byes, and since seed s meets seed (size + 1 − s) in round 1,
    /// the byes fall to seeds 1… — the first teams in the drawn order. A bye is won already: its team is
    /// placed straight into its round-2 slot. Matches come round by round, position by position.
    public static List<PlannedMatch> Knockout(IReadOnlyList<Guid> drawn)
    {
        var n = drawn.Count;
        if (n < 2) throw new ArgumentException("A knockout needs at least two teams.", nameof(drawn));
        var size = (int)BitOperations.RoundUpToPowerOf2((uint)n);
        var rounds = BitOperations.Log2((uint)size);
        var seeds = SeedOrder(size);

        // First list index of each round: round r has size / 2^r matches.
        var offset = new int[rounds + 2];
        for (var r = 1; r <= rounds; r++) offset[r + 1] = offset[r] + (size >> r);

        var matches = new List<PlannedMatch>();
        for (var r = 1; r <= rounds; r++)
        {
            for (var p = 1; p <= size >> r; p++)
            {
                // The winner of positions 2k-1 and 2k meet at position k of the next round, in slots A and B.
                int? next = r < rounds ? offset[r + 1] + (p + 1) / 2 - 1 : null;
                var slot = r < rounds ? (p % 2 == 1 ? "A" : "B") : null;
                Guid? a = null, b = null;
                if (r == 1)
                {
                    // The lower seed of a pair always comes first, so a bye is always slot B.
                    int seedA = seeds[2 * p - 2], seedB = seeds[2 * p - 1];
                    a = drawn[seedA - 1];
                    b = seedB <= n ? drawn[seedB - 1] : null;
                }
                matches.Add(new PlannedMatch(r, p, null, a, b, r == 1 && b is null, next, slot));
            }
        }

        // Advance the bye teams.
        foreach (var bye in matches.Where(m => m.IsBye).ToList())
        {
            var i = bye.Next!.Value;
            matches[i] = bye.NextSlot == "A" ? matches[i] with { TeamA = bye.TeamA } : matches[i] with { TeamB = bye.TeamA };
        }
        return matches;
    }

    // ---------- round robin ----------

    /// The venue's default: one group up to 6 teams, else groups of about four.
    public static int DefaultGroupCount(int teams) => teams <= 6 ? 1 : (teams + 3) / 4;

    /// Deals the drawn order into groups snake-wise (1,2,3,3,2,1,1,2,…), so group sizes differ by at most one.
    public static List<List<Guid>> Snake(IReadOnlyList<Guid> drawn, int groupCount)
    {
        var groups = Enumerable.Range(0, groupCount).Select(_ => new List<Guid>()).ToList();
        for (var i = 0; i < drawn.Count; i++)
        {
            var row = i / groupCount;
            var col = i % groupCount;
            groups[row % 2 == 0 ? col : groupCount - 1 - col].Add(drawn[i]);
        }
        return groups;
    }

    /// Every pair once, by the circle method: fix the first team, rotate the rest one place each round.
    /// An odd group gets a phantom team; whoever meets it sits that round out. Rounds are 1-based.
    public static List<(int Round, Guid A, Guid B)> CircleRounds(IReadOnlyList<Guid> teams)
    {
        var slots = teams.Select(t => (Guid?)t).ToList();
        if (slots.Count % 2 == 1) slots.Add(null);
        var m = slots.Count;
        var fixtures = new List<(int, Guid, Guid)>();
        for (var r = 0; r < m - 1; r++)
        {
            for (var i = 0; i < m / 2; i++)
            {
                if (slots[i] is not { } a || slots[m - 1 - i] is not { } b) continue;
                // The fixed team would otherwise always be listed first; alternate it.
                fixtures.Add(i == 0 && r % 2 == 1 ? (r + 1, b, a) : (r + 1, a, b));
            }
            var last = slots[m - 1];
            slots.RemoveAt(m - 1);
            slots.Insert(1, last);
        }
        return fixtures;
    }

    /// All group fixtures, round by round; within a round, group 1's matches first. Position runs across groups.
    public static List<PlannedMatch> RoundRobin(IReadOnlyList<Guid> drawn, int groupCount)
    {
        var byGroup = Snake(drawn, groupCount).Select((g, i) => (No: i + 1, Fixtures: CircleRounds(g))).ToList();
        var matches = new List<PlannedMatch>();
        var rounds = byGroup.Max(g => g.Fixtures.Select(f => f.Round).DefaultIfEmpty(0).Max());
        for (var r = 1; r <= rounds; r++)
        {
            var position = 0;
            foreach (var (no, fixtures) in byGroup)
                foreach (var f in fixtures.Where(f => f.Round == r))
                    matches.Add(new PlannedMatch(r, ++position, no, f.A, f.B, false, null, null));
        }
        return matches;
    }

    // ---------- schedule ----------

    public sealed record ScheduleItem(int Round, Guid? TeamA, Guid? TeamB);

    /// Greedy: matches in the given order (round by round), each onto the court that can start it soonest
    /// (ties → the earlier court in the list). A match never starts before its teams' previous match ends, and
    /// with roundBarrier (knockout, where later teams aren't known yet) a round never starts before the
    /// previous round's last match ends. Returns (court index, start) per match, in input order.
    /// ponytail: ignores opening hours and nights — a multi-day tournament schedules straight through; the venue reschedules.
    public static List<(int Court, DateTimeOffset StartsAt)> Schedule(
        IReadOnlyList<ScheduleItem> matches, int courtCount, DateTimeOffset start, int minutes, bool roundBarrier)
    {
        var length = TimeSpan.FromMinutes(minutes);
        var courtFree = Enumerable.Repeat(start, courtCount).ToArray();
        var teamFree = new Dictionary<Guid, DateTimeOffset>();
        var roundEnd = new Dictionary<int, DateTimeOffset>();
        var result = new List<(int, DateTimeOffset)>();
        foreach (var m in matches)
        {
            var earliest = start;
            if (roundBarrier && roundEnd.TryGetValue(m.Round - 1, out var previous)) earliest = Max(earliest, previous);
            foreach (var team in new[] { m.TeamA, m.TeamB })
                if (team is { } id && teamFree.TryGetValue(id, out var free)) earliest = Max(earliest, free);

            var court = 0;
            for (var c = 1; c < courtCount; c++)
                if (Max(courtFree[c], earliest) < Max(courtFree[court], earliest)) court = c;
            var at = Max(courtFree[court], earliest);
            var end = at + length;

            courtFree[court] = end;
            if (m.TeamA is { } ta) teamFree[ta] = end;
            if (m.TeamB is { } tb) teamFree[tb] = end;
            roundEnd[m.Round] = roundEnd.TryGetValue(m.Round, out var e) ? Max(e, end) : end;
            result.Add((court, at));
        }
        return result;
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    // ---------- standings ----------

    public sealed record Result(Guid TeamA, Guid TeamB, int ScoreA, int ScoreB);

    /// Also the JSON row of a group table.
    public sealed record Standing(
        Guid TeamId, string Name, int Played, int Won, int Drawn, int Lost, int ScoreFor, int ScoreAgainst, int Points);

    /// Win 3, draw 1. Order: points, score difference, score for, then head-to-head, then name.
    /// Head-to-head = points from the matches among just the teams still tied, which is the plain
    /// head-to-head result for two teams and stays a consistent order for three or more.
    public static List<Standing> Standings(IReadOnlyList<(Guid Id, string Name)> teams, IReadOnlyList<Result> results)
    {
        var table = teams.Select(t => Row(t.Id, t.Name, results)).ToList();
        return table
            .GroupBy(s => (s.Points, Diff: s.ScoreFor - s.ScoreAgainst, s.ScoreFor))
            .OrderByDescending(g => g.Key.Points).ThenByDescending(g => g.Key.Diff).ThenByDescending(g => g.Key.ScoreFor)
            .SelectMany(g =>
            {
                var tied = g.Select(s => s.TeamId).ToHashSet();
                var among = results.Where(r => tied.Contains(r.TeamA) && tied.Contains(r.TeamB)).ToList();
                return g.OrderByDescending(s => Row(s.TeamId, s.Name, among).Points).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase);
            })
            .ToList();
    }

    private static Standing Row(Guid id, string name, IReadOnlyList<Result> results)
    {
        int played = 0, won = 0, drawn = 0, lost = 0, scored = 0, conceded = 0;
        foreach (var r in results)
        {
            int mine, theirs;
            if (r.TeamA == id) (mine, theirs) = (r.ScoreA, r.ScoreB);
            else if (r.TeamB == id) (mine, theirs) = (r.ScoreB, r.ScoreA);
            else continue;
            played++;
            scored += mine;
            conceded += theirs;
            if (mine > theirs) won++;
            else if (mine == theirs) drawn++;
            else lost++;
        }
        return new Standing(id, name, played, won, drawn, lost, scored, conceded, won * 3 + drawn);
    }
}
