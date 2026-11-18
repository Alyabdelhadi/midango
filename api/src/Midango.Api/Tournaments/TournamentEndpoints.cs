using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Bookings;
using Midango.Api.Data;
using Midango.Api.Identity;
using Midango.Api.Notifications;
using Midango.Api.Venues;

namespace Midango.Api.Tournaments;

/// Venues set tournaments up in Business (publishing blocks their courts); captains sign teams up in the app;
/// the desk checks teams in and takes the entry fee. Times in every DTO carry the venue's offset.
public static class TournamentEndpoints
{
    public const int MaxCourts = 20;
    public const int MaxDays = 3;
    public const string LockedAfterPublish = "Cancel and create a new tournament to change this.";

    /// TeamSize null on create = half the sport's players per match.
    public sealed record TournamentBody(
        Guid CategoryId, string? NameEn, string? NameAr, string? DescriptionEn, string? DescriptionAr,
        TournamentFormat Format, int? TeamSize, int MaxTeams, decimal EntryFee, PlayLevel? Level, Guid[]? CourtIds,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset RegistrationClosesAt);

    public sealed record TeamBody(string? Name, string[]? Members);

    public sealed record CourtNameDto(Guid Id, string NameEn, string NameAr);

    public sealed record CaptainDto(string? Name, string MaskedPhone);

    /// Members = teammates only (positions 1..n); the captain is in Captain.
    public sealed record StaffTeamDto(
        Guid Id, string Name, TeamStatus Status, DateTimeOffset RegisteredAt, CaptainDto Captain,
        IReadOnlyList<string> Members, DateTimeOffset? CheckedInAt, decimal? CashCollected, int? DrawPosition, int? GroupNo);

    public sealed record VenueTournamentRowDto(
        Guid Id, string NameEn, string NameAr, Guid CategoryId, TournamentFormat Format, TournamentStatus Status,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset RegistrationClosesAt,
        int MaxTeams, int Registered, int Waitlisted, decimal EntryFee, string Currency);

    /// Teams: Registered by time, then Waitlisted by time, then Withdrawn.
    public sealed record VenueTournamentDto(
        Guid Id, Guid CategoryId, string NameEn, string NameAr, string? DescriptionEn, string? DescriptionAr,
        TournamentFormat Format, int TeamSize, int MaxTeams, decimal EntryFee, string Currency, PlayLevel? Level,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset RegistrationClosesAt,
        TournamentStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? PublishedAt, DateTimeOffset? CancelledAt,
        int Registered, int Waitlisted, IReadOnlyList<CourtNameDto> Courts, IReadOnlyList<StaffTeamDto> Teams,
        int? MatchMinutes, int? GroupCount, DateTimeOffset? DrawnAt, Guid? ChampionTeamId, bool OverrunsEnd,
        IReadOnlyList<MatchDto> Matches, IReadOnlyList<GroupDto> Groups);

    public sealed record TeamRefDto(Guid Id, string Name);

    /// TeamA/TeamB null = not known yet (a later knockout round) or the empty side of a bye.
    public sealed record MatchDto(
        Guid Id, MatchStage Stage, int Round, int Position, int? GroupNo, TeamRefDto? TeamA, TeamRefDto? TeamB,
        int? ScoreA, int? ScoreB, Guid? WinnerTeamId, bool IsBye, Guid? CourtId, string? CourtNameEn, string? CourtNameAr,
        DateTimeOffset? StartsAt, Guid? NextMatchId, string? NextSlot);

    public sealed record GroupDto(int GroupNo, IReadOnlyList<TournamentDraw.Standing> Standings);

    /// MatchMinutes null = 60. GroupCount: round robin only; null = the default for the number of teams.
    public sealed record DrawBody(int? MatchMinutes, int? GroupCount);

    public sealed record ResultBody(int? ScoreA, int? ScoreB);

    public sealed record ScheduleBody(Guid CourtId, DateTimeOffset StartsAt);

    public sealed record ClashDto(string CourtNameEn, string CourtNameAr, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reference);

    public sealed record TournamentCardDto(
        Guid Id, Guid VenueId, string VenueNameEn, string VenueNameAr, string? VenueLogoUrl, Guid CategoryId,
        string NameEn, string NameAr, TournamentFormat Format, int TeamSize,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset RegistrationClosesAt,
        decimal EntryFee, string Currency, int MaxTeams, int Registered, int Waitlisted, PlayLevel? Level,
        bool RegistrationOpen, TournamentStatus Status);

    /// Public team: first names only, captain first. Registered teams only.
    public sealed record PublicTeamDto(string Name, IReadOnlyList<string> Members);

    public sealed record TournamentPageDto(
        Guid Id, Guid VenueId, string VenueNameEn, string VenueNameAr, string? VenueLogoUrl, Guid CategoryId,
        string NameEn, string NameAr, TournamentFormat Format, int TeamSize,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset RegistrationClosesAt,
        decimal EntryFee, string Currency, int MaxTeams, int Registered, int Waitlisted, PlayLevel? Level,
        bool RegistrationOpen, TournamentStatus Status,
        string? DescriptionEn, string? DescriptionAr, string? AddressEn, string? AddressAr,
        IReadOnlyList<CourtNameDto> Courts, IReadOnlyList<PublicTeamDto> Teams,
        IReadOnlyList<MatchDto> Matches, IReadOnlyList<GroupDto> Groups, string? ChampionTeamName);

    /// The captain's own team: full names, captain first. WaitlistPosition is 1-based, null unless waitlisted.
    public sealed record MyTeamDto(
        Guid Id, string Name, TeamStatus Status, IReadOnlyList<string> Members, DateTimeOffset? CheckedInAt, int? WaitlistPosition,
        MatchDto? NextMatch);

    public sealed record TeamResultDto(MyTeamDto Team, TournamentPageDto Tournament);

    public sealed record MyTournamentDto(TournamentCardDto Tournament, MyTeamDto Team);

    public static void MapTournamentEndpoints(this IEndpointRouteBuilder app)
    {
        var venue = app.MapGroup("/venue/tournaments").RequireAuthorization(Policies.VenueStaff);

        venue.MapGet("", async (ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var loaded = await LoadAsync(db, db.Tournaments.Where(t => t.VenueId == venueId), ct);
            return Results.Ok(loaded.OrderByDescending(l => l.T.CreatedAt).Select(l => new VenueTournamentRowDto(
                l.T.Id, l.T.NameEn, l.T.NameAr, l.T.CategoryId, l.T.Format, l.T.Status,
                l.Local(l.T.StartsAt), l.Local(l.T.EndsAt), l.Local(l.T.RegistrationClosesAt),
                l.T.MaxTeams, l.Count(TeamStatus.Registered), l.Count(TeamStatus.Waitlisted), l.T.EntryFee, l.T.Currency)));
        });

        venue.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
            await VenueDetailAsync(db, user, id, ct) is { } dto ? Results.Ok(dto) : ApiErrors.Fail(404, "Tournament not found."));

        venue.MapPost("", async (TournamentBody body, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var place = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == user.StaffVenueId()!.Value, ct);
            var category = await db.SportCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == body.CategoryId && c.Active, ct);
            if (category is null) return ApiErrors.Fail(400, "Choose a sport.");
            var teamSize = body.TeamSize ?? Math.Max(1, category.PlayersPerMatch / 2);
            if (await ValidateAsync(db, place.Id, body, teamSize, clock.GetUtcNow(), ct) is { } error) return ApiErrors.Fail(400, error);

            var tournament = new Tournament
            {
                VenueId = place.Id,
                NameEn = "",
                NameAr = "",
                Currency = place.CurrencyCode,
                CreatedAt = clock.GetUtcNow(),
            };
            Apply(tournament, body, teamSize);
            db.Tournaments.Add(tournament);
            await db.SaveChangesAsync(ct);
            return Results.Json(await VenueDetailAsync(db, user, tournament.Id, ct), statusCode: 201);
        }).RequireAuthorization(Policies.VenueManager);

        venue.MapPut("/{id:guid}", async (
            Guid id, TournamentBody body, ClaimsPrincipal user, MidangoDb db, TournamentNotifications notify, TimeProvider clock, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var now = clock.GetUtcNow();
            List<Guid> promoted;
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                var t = await LockAsync(db, id, ct);
                if (t is null || t.VenueId != venueId) return ApiErrors.Fail(404, "Tournament not found.");
                if (t.Status == TournamentStatus.Cancelled) return ApiErrors.Fail(409, "This tournament was cancelled.");

                if (t.Status == TournamentStatus.Draft)
                {
                    if (!await db.SportCategories.AnyAsync(c => c.Id == body.CategoryId && c.Active, ct))
                        return ApiErrors.Fail(400, "Choose a sport.");
                    var teamSize = body.TeamSize ?? t.TeamSize;
                    if (await ValidateAsync(db, venueId, body, teamSize, now, ct) is { } error) return ApiErrors.Fail(400, error);
                    Apply(t, body, teamSize);
                    promoted = [];
                }
                else
                {
                    var courts = (body.CourtIds ?? []).Distinct().ToHashSet();
                    if (body.CategoryId != t.CategoryId || body.Format != t.Format || (body.TeamSize ?? t.TeamSize) != t.TeamSize
                        || body.EntryFee != t.EntryFee || body.Level != t.Level || !courts.SetEquals(t.CourtIds)
                        || body.StartsAt.ToUniversalTime() != t.StartsAt || body.EndsAt.ToUniversalTime() != t.EndsAt)
                        return ApiErrors.Fail(400, LockedAfterPublish);
                    if (TextError(body) is { } textError) return ApiErrors.Fail(400, textError);
                    if (body.MaxTeams is < 2 or > 64) return ApiErrors.Fail(400, "Allow 2 to 64 teams.");
                    var closes = body.RegistrationClosesAt.ToUniversalTime();
                    if (closes > t.StartsAt) return ApiErrors.Fail(400, "Registration must close by the start.");

                    var teams = await db.TournamentTeams.Where(x => x.TournamentId == id && x.Status != TeamStatus.Withdrawn).ToListAsync(ct);
                    var registered = teams.Count(x => x.Status == TeamStatus.Registered);
                    if (body.MaxTeams < registered)
                        return ApiErrors.Fail(400, $"{registered} teams are registered. Allow at least {registered}.");

                    var nameEn = body.NameEn!.Trim();
                    if (nameEn != t.NameEn)
                        await db.Bookings.Where(b => b.TournamentId == id)
                            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Note, BlockNote(nameEn)), ct);
                    t.NameEn = nameEn;
                    t.NameAr = body.NameAr!.Trim();
                    t.DescriptionEn = Blank(body.DescriptionEn);
                    t.DescriptionAr = Blank(body.DescriptionAr);
                    t.RegistrationClosesAt = closes;
                    t.MaxTeams = body.MaxTeams;
                    // Once drawn, a promoted team would be registered but in no match.
                    promoted = t.Status == TournamentStatus.Published ? Promote(t, teams) : [];
                }
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            await notify.PromotedAsync(id, promoted);
            return Results.Ok(await VenueDetailAsync(db, user, id, ct));
        }).RequireAuthorization(Policies.VenueManager);

        venue.MapPost("/{id:guid}/publish", async (Guid id, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var t = await db.Tournaments.SingleOrDefaultAsync(x => x.Id == id && x.VenueId == venueId, ct);
            if (t is null) return ApiErrors.Fail(404, "Tournament not found.");
            if (t.Status == TournamentStatus.Cancelled) return ApiErrors.Fail(409, "This tournament was cancelled.");
            if (t.Status != TournamentStatus.Draft) return Results.Ok(await VenueDetailAsync(db, user, id, ct));
            var now = clock.GetUtcNow();
            if (t.StartsAt <= now) return ApiErrors.Fail(409, "The start has already passed. Change the times first.");

            var blocks = t.CourtIds.Select(courtId => new Booking
            {
                Reference = BookingReference.New(),
                Kind = BookingKind.Block,
                Source = BookingSource.Staff,
                CreatedByStaffId = user.SubjectId(),
                VenueId = venueId,
                CourtId = courtId,
                TournamentId = t.Id,
                Note = BlockNote(t.NameEn),
                StartsAt = t.StartsAt,
                EndsAt = t.EndsAt,
                Minutes = (int)(t.EndsAt - t.StartsAt).TotalMinutes,
                Currency = t.Currency,
                CreatedAt = now,
                FreeCancellationUntil = t.StartsAt,
            }).ToList();
            db.Bookings.AddRange(blocks);
            t.Status = TournamentStatus.Published;
            t.PublishedAt = now;

            // One SaveChanges = one transaction: a clash on any court publishes nothing.
            // ponytail: two simultaneous publishes of the same draft: the second clashes with the first's blocks. Lock if that ever matters.
            if (await BookingStore.SaveAsync(db, blocks, ct) is not null)
            {
                var place = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == venueId, ct);
                var zone = VenueClock.Zone(place);
                var clashes = await (from b in db.Bookings
                                     join c in db.Courts on b.CourtId equals c.Id
                                     where t.CourtIds.Contains(b.CourtId) && b.Status == BookingStatus.Confirmed
                                           && b.StartsAt < t.EndsAt && b.EndsAt > t.StartsAt
                                     orderby b.StartsAt, c.SortOrder
                                     select new { c.NameEn, c.NameAr, b.StartsAt, b.EndsAt, b.Reference })
                    .AsNoTracking().ToListAsync(ct);
                return Results.Problem(statusCode: 409, title: "These courts are already booked during the tournament.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["clashes"] = clashes.Select(c => new ClashDto(c.NameEn, c.NameAr,
                            TimeZoneInfo.ConvertTime(c.StartsAt, zone), TimeZoneInfo.ConvertTime(c.EndsAt, zone), c.Reference)).ToList(),
                    });
            }
            return Results.Ok(await VenueDetailAsync(db, user, id, ct));
        }).RequireAuthorization(Policies.VenueManager);

        venue.MapPost("/{id:guid}/cancel", async (
            Guid id, ClaimsPrincipal user, MidangoDb db, TournamentNotifications notify, TimeProvider clock, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var now = clock.GetUtcNow();
            List<Guid> captains;
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                var t = await LockAsync(db, id, ct);
                if (t is null || t.VenueId != venueId) return ApiErrors.Fail(404, "Tournament not found.");
                if (t.Status == TournamentStatus.Cancelled) return Results.Ok(await VenueDetailAsync(db, user, id, ct));
                if (t.Status == TournamentStatus.Finished || t.EndsAt <= now) return ApiErrors.Fail(409, "This tournament has already finished.");

                t.Status = TournamentStatus.Cancelled;
                t.CancelledAt = now;
                var blocks = await db.Bookings.Where(b => b.TournamentId == id && b.Status == BookingStatus.Confirmed).ToListAsync(ct);
                foreach (var b in blocks)
                {
                    b.Status = BookingStatus.Cancelled;
                    b.CancelledAt = now;
                    b.CancelledByVenue = true;
                }
                captains = await db.TournamentTeams.Where(x => x.TournamentId == id && x.Status != TeamStatus.Withdrawn)
                    .Select(x => x.CaptainCustomerId).ToListAsync(ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            await notify.CancelledAsync(id, captains);
            return Results.Ok(await VenueDetailAsync(db, user, id, ct));
        }).RequireAuthorization(Policies.VenueManager);

        venue.MapPost("/{id:guid}/teams/{teamId:guid}/check-in", (
            Guid id, Guid teamId, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangeTeamAsync(db, user, id, teamId, ct, (t, team) =>
            {
                if (team.CheckedInAt is not null) return null; // already done: same answer again
                if (t.Status is not (TournamentStatus.Published or TournamentStatus.InProgress))
                    return "Only a published tournament can check teams in.";
                if (team.Status != TeamStatus.Registered) return "Only a registered team can be checked in.";
                var now = clock.GetUtcNow();
                if (now < t.StartsAt.AddMinutes(-DeskEndpoints.CheckInOpensMinutesBefore))
                    return $"Check-in opens {DeskEndpoints.CheckInOpensMinutesBefore} minutes before the start.";
                if (now > t.EndsAt) return "This tournament has finished.";
                team.CheckedInAt = now;
                // Entry fees earn no loyalty points in T1.
                team.CashCollected = t.EntryFee;
                return null;
            }));

        venue.MapPost("/{id:guid}/teams/{teamId:guid}/undo-check-in", (
            Guid id, Guid teamId, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            ChangeTeamAsync(db, user, id, teamId, ct, (_, team) =>
            {
                if (team.CheckedInAt is not { } at) return null;
                if (clock.GetUtcNow() - at > DeskEndpoints.UndoWindow) return "A check-in can only be undone within 15 minutes.";
                team.CheckedInAt = null;
                team.CashCollected = null;
                return null;
            }));

        venue.MapPost("/{id:guid}/draw", async (
            Guid id, DrawBody body, ClaimsPrincipal user, MidangoDb db, TournamentNotifications notify, TimeProvider clock, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var now = clock.GetUtcNow();
            var minutes = body.MatchMinutes ?? 60;
            if (minutes is < 15 or > 240) return ApiErrors.Fail(400, "Set a match length between 15 and 240 minutes.");
            List<TournamentMatch> matches;
            List<TournamentTeam> drawn;
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                var t = await LockAsync(db, id, ct);
                if (t is null || t.VenueId != venueId) return ApiErrors.Fail(404, "Tournament not found.");
                if (t.Status == TournamentStatus.Draft) return ApiErrors.Fail(409, "Publish the tournament first.");
                if (await PlayLockedAsync(db, t, ct) is { } locked) return ApiErrors.Fail(409, locked);
                if (now < t.RegistrationClosesAt) return ApiErrors.Fail(409, "Registration is still open.");

                var teams = await db.TournamentTeams.Where(x => x.TournamentId == id).ToListAsync(ct);
                drawn = teams.Where(x => x.Status == TeamStatus.Registered).ToList();
                var n = drawn.Count;
                if (n < 2) return ApiErrors.Fail(409, "A draw needs at least 2 registered teams.");
                int? groupCount = null;
                if (t.Format == TournamentFormat.RoundRobin)
                {
                    groupCount = body.GroupCount ?? TournamentDraw.DefaultGroupCount(n);
                    if (groupCount < 1 || groupCount > n / 2)
                        return ApiErrors.Fail(400, n / 2 == 1 ? $"{n} teams make 1 group." : $"Choose 1 to {n / 2} groups: each needs at least 2 teams.");
                }

                await ClearDrawAsync(db, t, teams, ct);
                var shuffled = drawn.ToArray();
                RandomNumberGenerator.Shuffle(shuffled.AsSpan());
                drawn = shuffled.ToList();
                for (var i = 0; i < n; i++) drawn[i].DrawPosition = i + 1;
                var order = drawn.Select(x => x.Id).ToList();

                var knockout = t.Format == TournamentFormat.Knockout;
                var planned = knockout ? TournamentDraw.Knockout(order) : TournamentDraw.RoundRobin(order, groupCount!.Value);
                var ids = planned.Select(_ => Guid.CreateVersion7()).ToList();
                matches = planned.Select((p, i) => new TournamentMatch
                {
                    Id = ids[i],
                    TournamentId = id,
                    Stage = knockout ? MatchStage.Knockout : MatchStage.Group,
                    Round = p.Round,
                    Position = p.Position,
                    GroupNo = p.GroupNo,
                    TeamAId = p.TeamA,
                    TeamBId = p.TeamB,
                    IsBye = p.IsBye,
                    WinnerTeamId = p.IsBye ? p.TeamA : null,
                    NextMatchId = p.Next is { } next ? ids[next] : null,
                    NextSlot = p.NextSlot,
                }).ToList();
                if (!knockout)
                {
                    var byId = drawn.ToDictionary(x => x.Id);
                    foreach (var m in matches) byId[m.TeamAId!.Value].GroupNo = byId[m.TeamBId!.Value].GroupNo = m.GroupNo;
                }

                var played = matches.Where(m => !m.IsBye).ToList();
                var slots = TournamentDraw.Schedule(
                    played.Select(m => new TournamentDraw.ScheduleItem(m.Round, m.TeamAId, m.TeamBId)).ToList(),
                    t.CourtIds.Length, t.StartsAt, minutes, roundBarrier: knockout);
                for (var i = 0; i < played.Count; i++)
                {
                    played[i].CourtId = t.CourtIds[slots[i].Court];
                    played[i].StartsAt = slots[i].StartsAt;
                }

                db.TournamentMatches.AddRange(matches);
                t.Status = TournamentStatus.InProgress;
                t.DrawnAt = now;
                t.MatchMinutes = minutes;
                t.GroupCount = groupCount;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }

            var names = drawn.ToDictionary(x => x.Id, x => x.Name);
            var courtIds = matches.Where(m => m.CourtId != null).Select(m => m.CourtId!.Value).Distinct().ToList();
            var courts = await db.Courts.AsNoTracking().Where(c => courtIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
            var notices = drawn.Select(x =>
            {
                if (matches.Any(m => m.IsBye && m.TeamAId == x.Id)) return new TournamentNotifications.DrawNotice(x.CaptainCustomerId, null, null, null, null);
                var first = matches.Where(m => !m.IsBye && (m.TeamAId == x.Id || m.TeamBId == x.Id)).MinBy(m => m.StartsAt)!;
                var court = courts[first.CourtId!.Value];
                return new TournamentNotifications.DrawNotice(x.CaptainCustomerId,
                    names[first.TeamAId == x.Id ? first.TeamBId!.Value : first.TeamAId!.Value], first.StartsAt, court.NameEn, court.NameAr);
            }).ToList();
            await notify.DrawAsync(id, notices);
            return Results.Ok(await VenueDetailAsync(db, user, id, ct));
        }).RequireAuthorization(Policies.VenueManager);

        // A no-show, say. No waitlist promotion: registration has closed. A drawn team takes the draw with it.
        venue.MapPost("/{id:guid}/teams/{teamId:guid}/withdraw", async (
            Guid id, Guid teamId, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                var t = await LockAsync(db, id, ct);
                if (t is null || t.VenueId != venueId) return ApiErrors.Fail(404, "Tournament not found.");
                var teams = await db.TournamentTeams.Where(x => x.TournamentId == id).ToListAsync(ct);
                var team = teams.FirstOrDefault(x => x.Id == teamId);
                if (team is null) return ApiErrors.Fail(404, "Team not found.");
                if (team.Status == TeamStatus.Withdrawn) return Results.Ok(await VenueDetailAsync(db, user, id, ct));
                if (await PlayLockedAsync(db, t, ct) is { } locked) return ApiErrors.Fail(409, locked);

                team.Status = TeamStatus.Withdrawn;
                team.WithdrawnAt = clock.GetUtcNow();
                if (team.DrawPosition is not null) await ClearDrawAsync(db, t, teams, ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            return Results.Ok(await VenueDetailAsync(db, user, id, ct));
        });

        venue.MapPut("/{id:guid}/matches/{matchId:guid}/result", (
            Guid id, Guid matchId, ResultBody body, ClaimsPrincipal user, MidangoDb db, TournamentNotifications notify, TimeProvider clock, CancellationToken ct) =>
            ChangeMatchAsync(db, user, notify, id, matchId, ct, (_, matches, m) =>
            {
                if (body.ScoreA is not (>= 0 and <= 99) || body.ScoreB is not (>= 0 and <= 99))
                    return ApiErrors.Fail(400, "Enter both scores, from 0 to 99.");
                if (m.IsBye) return ApiErrors.Fail(409, "A bye has no result.");
                if (m.TeamAId is null || m.TeamBId is null) return ApiErrors.Fail(409, "Both teams must be known before the result.");
                int a = body.ScoreA.Value, b = body.ScoreB.Value;
                if (m.ScoreA == a && m.ScoreB == b) return null; // the same result again
                if (m.Stage == MatchStage.Knockout && a == b) return ApiErrors.Fail(400, "A knockout match needs a winner. Enter different scores.");
                var next = matches.FirstOrDefault(x => x.Id == m.NextMatchId);
                if (next?.ScoreA is not null) return ApiErrors.Fail(409, "The next match already has a result. Clear that one first.");

                m.ScoreA = a;
                m.ScoreB = b;
                m.WinnerTeamId = a > b ? m.TeamAId : b > a ? m.TeamBId : null;
                m.ResultAt = clock.GetUtcNow();
                if (next is not null) FillSlot(next, m.NextSlot, m.WinnerTeamId);
                return null;
            }));

        venue.MapDelete("/{id:guid}/matches/{matchId:guid}/result", (
            Guid id, Guid matchId, ClaimsPrincipal user, MidangoDb db, TournamentNotifications notify, CancellationToken ct) =>
            ChangeMatchAsync(db, user, notify, id, matchId, ct, (_, matches, m) =>
            {
                if (m.ScoreA is null) return null;
                var next = matches.FirstOrDefault(x => x.Id == m.NextMatchId);
                if (next?.ScoreA is not null) return ApiErrors.Fail(409, "The next match already has a result. Clear that one first.");
                m.ScoreA = m.ScoreB = null;
                m.WinnerTeamId = null;
                m.ResultAt = null;
                if (next is not null) FillSlot(next, m.NextSlot, null);
                return null;
            }));

        venue.MapPut("/{id:guid}/matches/{matchId:guid}/schedule", (
            Guid id, Guid matchId, ScheduleBody body, ClaimsPrincipal user, MidangoDb db, TournamentNotifications notify, CancellationToken ct) =>
            ChangeMatchAsync(db, user, notify, id, matchId, ct, (t, matches, m) =>
            {
                if (m.IsBye) return ApiErrors.Fail(409, "A bye isn't played.");
                if (!t.CourtIds.Contains(body.CourtId)) return ApiErrors.Fail(400, "Choose one of the tournament's courts.");
                var start = body.StartsAt.ToUniversalTime();
                if (start < t.StartsAt || start > t.EndsAt) return ApiErrors.Fail(400, "Choose a time during the tournament.");
                var length = TimeSpan.FromMinutes(t.MatchMinutes ?? 60);
                if (matches.Any(x => x.Id != m.Id && x.CourtId == body.CourtId && x.StartsAt is { } s && s < start + length && s + length > start))
                    return ApiErrors.Fail(409, "Another match of this tournament is on that court then.");
                m.CourtId = body.CourtId;
                m.StartsAt = start;
                return null;
            }));

        // Public, like /matches.
        app.MapGet("/tournaments", async (Guid? categoryId, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow();
            var loaded = await LoadAsync(db, db.Tournaments.Where(t =>
                t.Status != TournamentStatus.Draft && t.Status != TournamentStatus.Cancelled && t.EndsAt > now
                && (categoryId == null || t.CategoryId == categoryId)
                && db.Venues.Any(v => v.Id == t.VenueId && v.Published)), ct);
            return Results.Ok(loaded.OrderBy(l => l.T.StartsAt).Select(l => Card(l, now)));
        });

        app.MapGet("/tournaments/{id:guid}", async (Guid id, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            await PageAsync(db, id, clock.GetUtcNow(), ct) is { } page ? Results.Ok(page) : ApiErrors.Fail(404, "Tournament not found."));

        var customer = app.MapGroup("").RequireAuthorization(Policies.Customer);

        customer.MapPost("/tournaments/{id:guid}/teams", async (
            Guid id, TeamBody body, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            var me = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == customerId, ct);
            if (me is null) return ApiErrors.Fail(404, "Account not found.");
            if (await Customers.BlockedAsync(db, customerId, ct) is { } blocked) return blocked;
            if (me.Name is null) return ApiErrors.Fail(409, "Add your name before registering. The venue uses it at the desk.");
            var name = body.Name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 40) return ApiErrors.Fail(400, "Enter a team name, up to 40 characters.");
            var members = (body.Members ?? []).Select(m => m?.Trim() ?? "").ToList();
            if (members.Any(m => m.Length is 0 or > 60)) return ApiErrors.Fail(400, "Enter each teammate's name, up to 60 characters.");

            var now = clock.GetUtcNow();
            TournamentTeam team;
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                // Locked until commit, so two registrations for the last spot can't both see it free.
                var t = await LockAsync(db, id, ct);
                if (t is not { Status: TournamentStatus.Published } || !await db.Venues.AnyAsync(v => v.Id == t.VenueId && v.Published, ct))
                    return ApiErrors.Fail(404, "Tournament not found.");
                if (now >= t.RegistrationClosesAt) return ApiErrors.Fail(409, "Registration for this tournament has closed.");
                if (members.Count != t.TeamSize - 1)
                    return ApiErrors.Fail(400, t.TeamSize == 1
                        ? "This tournament is for single players. Don't add teammates."
                        : $"Add {t.TeamSize - 1} teammate{(t.TeamSize == 2 ? "" : "s")}: teams here have {t.TeamSize} players.");

                var teams = await db.TournamentTeams.Where(x => x.TournamentId == id && x.Status != TeamStatus.Withdrawn).ToListAsync(ct);
                if (teams.Any(x => x.CaptainCustomerId == customerId)) return ApiErrors.Fail(409, "You've already registered a team for this tournament.");
                if (teams.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return ApiErrors.Fail(409, "Another team already has that name. Choose a different one.");

                team = new TournamentTeam
                {
                    TournamentId = id,
                    Name = name,
                    CaptainCustomerId = customerId,
                    Status = teams.Count(x => x.Status == TeamStatus.Registered) < t.MaxTeams ? TeamStatus.Registered : TeamStatus.Waitlisted,
                    RegisteredAt = now,
                };
                team.Members = [new() { TeamId = team.Id, Position = 0, Name = me.Name, CustomerId = customerId },
                    .. members.Select((m, i) => new TournamentTeamMember { TeamId = team.Id, Position = i + 1, Name = m })];
                db.TournamentTeams.Add(team);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            return Results.Json(await TeamResultAsync(db, id, team.Id, now, ct), statusCode: 201);
        });

        customer.MapPost("/tournaments/{id:guid}/teams/{teamId:guid}/withdraw", async (
            Guid id, Guid teamId, ClaimsPrincipal user, MidangoDb db, TournamentNotifications notify, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            var now = clock.GetUtcNow();
            List<Guid> promoted = [];
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                var t = await LockAsync(db, id, ct);
                var teams = await db.TournamentTeams.Where(x => x.TournamentId == id && x.Status != TeamStatus.Withdrawn).ToListAsync(ct);
                var team = teams.FirstOrDefault(x => x.Id == teamId && x.CaptainCustomerId == customerId);
                if (t is null || team is null)
                {
                    // Withdrawing twice gives the same answer again.
                    if (t is not null && await db.TournamentTeams.AnyAsync(x => x.Id == teamId && x.TournamentId == id
                            && x.CaptainCustomerId == customerId && x.Status == TeamStatus.Withdrawn, ct))
                        return Results.Ok(await TeamResultAsync(db, id, teamId, now, ct));
                    return ApiErrors.Fail(404, "Team not found.");
                }
                if (team.CheckedInAt is not null) return ApiErrors.Fail(409, "Your team has checked in at the venue.");
                if (team.DrawPosition is not null) return ApiErrors.Fail(409, "The draw has been made. Ask the venue to withdraw your team.");
                if (t.StartsAt <= now) return ApiErrors.Fail(409, "This tournament has already started.");

                var wasRegistered = team.Status == TeamStatus.Registered;
                team.Status = TeamStatus.Withdrawn;
                team.WithdrawnAt = now;
                if (wasRegistered && t.Status == TournamentStatus.Published) promoted = Promote(t, teams);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            await notify.PromotedAsync(id, promoted);
            return Results.Ok(await TeamResultAsync(db, id, teamId, now, ct));
        });

        // Upcoming first (soonest), then finished ones (latest first). Withdrawn teams are left out.
        customer.MapGet("/me/tournaments", async (ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            var now = clock.GetUtcNow();
            var loaded = await LoadAsync(db, db.Tournaments.Where(t =>
                t.Status != TournamentStatus.Draft
                && db.TournamentTeams.Any(x => x.TournamentId == t.Id && x.CaptainCustomerId == customerId && x.Status != TeamStatus.Withdrawn)), ct);
            return Results.Ok(loaded
                .OrderBy(l => l.T.EndsAt <= now)
                .ThenBy(l => l.T.EndsAt > now ? l.T.StartsAt.UtcTicks : -l.T.StartsAt.UtcTicks)
                .Select(l => new MyTournamentDto(Card(l, now),
                    MyTeam(l, l.Teams.First(x => x.CaptainCustomerId == customerId && x.Status != TeamStatus.Withdrawn)))));
        });
    }

    /// Promotes the oldest waitlisted teams while there's room. Returns their captains, to push after commit.
    private static List<Guid> Promote(Tournament t, List<TournamentTeam> teams)
    {
        var free = t.MaxTeams - teams.Count(x => x.Status == TeamStatus.Registered);
        var promoted = teams.Where(x => x.Status == TeamStatus.Waitlisted).OrderBy(x => x.RegisteredAt).Take(Math.Max(0, free)).ToList();
        foreach (var x in promoted) x.Status = TeamStatus.Registered;
        return promoted.Select(x => x.CaptainCustomerId).ToList();
    }

    private static async Task<Tournament?> LockAsync(MidangoDb db, Guid id, CancellationToken ct) =>
        (await db.Tournaments.FromSql($"SELECT * FROM tournaments WHERE id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();

    /// Why the draw (and the drawn teams) can no longer change, or null.
    private static async Task<string?> PlayLockedAsync(MidangoDb db, Tournament t, CancellationToken ct)
    {
        if (t.Status == TournamentStatus.Cancelled) return "This tournament was cancelled.";
        if (t.Status == TournamentStatus.Finished || await db.TournamentMatches.AnyAsync(m => m.TournamentId == t.Id && m.ScoreA != null, ct))
            return "Results have been entered, so the draw can't change.";
        return null;
    }

    /// Deletes the matches and puts the tournament back to Published. Teams must be all of its tracked teams.
    private static async Task ClearDrawAsync(MidangoDb db, Tournament t, List<TournamentTeam> teams, CancellationToken ct)
    {
        await db.TournamentMatches.Where(m => m.TournamentId == t.Id).ExecuteDeleteAsync(ct);
        foreach (var x in teams) x.DrawPosition = x.GroupNo = null;
        if (t.Status == TournamentStatus.InProgress) t.Status = TournamentStatus.Published;
        t.DrawnAt = null;
        t.MatchMinutes = null;
        t.GroupCount = null;
        t.ChampionTeamId = null;
    }

    private static void FillSlot(TournamentMatch next, string? slot, Guid? team)
    {
        if (slot == "A") next.TeamAId = team;
        else next.TeamBId = team;
    }

    /// Loads the tournament (locked) and all its matches, applies change (an error result, or null to save), then
    /// settles status and champion. A new champion is pushed after commit.
    private static async Task<IResult> ChangeMatchAsync(
        MidangoDb db, ClaimsPrincipal user, TournamentNotifications notify, Guid id, Guid matchId, CancellationToken ct,
        Func<Tournament, List<TournamentMatch>, TournamentMatch, IResult?> change)
    {
        var venueId = user.StaffVenueId()!.Value;
        (string Team, List<Guid> Captains)? champion = null;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var t = await LockAsync(db, id, ct);
            if (t is null || t.VenueId != venueId) return ApiErrors.Fail(404, "Tournament not found.");
            if (t.Status == TournamentStatus.Cancelled) return ApiErrors.Fail(409, "This tournament was cancelled.");
            var matches = await db.TournamentMatches.Where(m => m.TournamentId == id).ToListAsync(ct);
            var match = matches.FirstOrDefault(m => m.Id == matchId);
            if (match is null) return ApiErrors.Fail(404, "Match not found.");
            if (change(t, matches, match) is { } error) return error;

            var drawn = await db.TournamentTeams.AsNoTracking().Where(x => x.TournamentId == id && x.DrawPosition != null).ToListAsync(ct);
            var before = t.ChampionTeamId;
            Settle(t, matches, drawn);
            if (t.ChampionTeamId is { } c && c != before)
                champion = (drawn.Single(x => x.Id == c).Name, drawn.Select(x => x.CaptainCustomerId).ToList());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        if (champion is { } won) await notify.ChampionAsync(id, won.Team, won.Captains);
        return Results.Ok(await VenueDetailAsync(db, user, id, ct));
    }

    /// Finished with a champion once every played match has a result; otherwise back in progress.
    private static void Settle(Tournament t, List<TournamentMatch> matches, List<TournamentTeam> drawn)
    {
        if (matches.Any(m => !m.IsBye && m.ScoreA is null))
        {
            t.Status = TournamentStatus.InProgress;
            t.ChampionTeamId = null;
            return;
        }
        t.Status = TournamentStatus.Finished;
        t.ChampionTeamId = t.Format == TournamentFormat.Knockout
            ? matches.MaxBy(m => m.Round)!.WinnerTeamId
            : t.GroupCount == 1 ? TournamentDraw.Standings(drawn.Select(x => (x.Id, x.Name)).ToList(), ScoredResults(matches))[0].TeamId : null;
    }

    private static List<TournamentDraw.Result> ScoredResults(IEnumerable<TournamentMatch> matches) =>
        matches.Where(m => m is { ScoreA: not null, ScoreB: not null, TeamAId: not null, TeamBId: not null })
            .Select(m => new TournamentDraw.Result(m.TeamAId!.Value, m.TeamBId!.Value, m.ScoreA!.Value, m.ScoreB!.Value)).ToList();

    private static async Task<IResult> ChangeTeamAsync(
        MidangoDb db, ClaimsPrincipal user, Guid id, Guid teamId, CancellationToken ct, Func<Tournament, TournamentTeam, string?> change)
    {
        var venueId = user.StaffVenueId()!.Value;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var t = await LockAsync(db, id, ct);
            if (t is null || t.VenueId != venueId) return ApiErrors.Fail(404, "Tournament not found.");
            var team = await db.TournamentTeams.SingleOrDefaultAsync(x => x.Id == teamId && x.TournamentId == id, ct);
            if (team is null) return ApiErrors.Fail(404, "Team not found.");
            if (change(t, team) is { } error) return ApiErrors.Fail(409, error);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return Results.Ok(await VenueDetailAsync(db, user, id, ct));
    }

    private static string BlockNote(string nameEn) => $"Tournament: {nameEn}";

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TextError(TournamentBody body)
    {
        if (body.NameEn?.Trim() is not { Length: >= 1 and <= 120 } || body.NameAr?.Trim() is not { Length: >= 1 and <= 120 })
            return "Enter the name in English and Arabic, up to 120 characters each.";
        if (Blank(body.DescriptionEn) is { Length: > 2000 } || Blank(body.DescriptionAr) is { Length: > 2000 })
            return "Keep each description under 2000 characters.";
        return null;
    }

    /// Everything a draft must satisfy. The category is checked by the caller. Returns an error sentence or null.
    private static async Task<string?> ValidateAsync(
        MidangoDb db, Guid venueId, TournamentBody body, int teamSize, DateTimeOffset now, CancellationToken ct)
    {
        if (TextError(body) is { } textError) return textError;
        if (!Enum.IsDefined(body.Format)) return "Choose a format.";
        if (body.Level is { } level && !Enum.IsDefined(level)) return "Choose a level.";
        if (teamSize is < 1 or > 11) return "Choose a team size between 1 and 11 players.";
        if (body.MaxTeams is < 2 or > 64) return "Allow 2 to 64 teams.";
        if (body.EntryFee is < 0 or > 10_000) return "Set an entry fee between 0 and 10,000.";

        var start = body.StartsAt.ToUniversalTime();
        var end = body.EndsAt.ToUniversalTime();
        // Range checks first: an absurd date (year 9999) would overflow the time-zone conversion.
        if (start <= now) return "The start must be in the future.";
        if (start > now.AddDays(365)) return "Start within the next year.";
        if (end <= start) return "The end must be after the start.";
        if (end > start.AddDays(MaxDays)) return $"A tournament can last up to {MaxDays} days.";
        if (body.RegistrationClosesAt.ToUniversalTime() > start) return "Registration must close by the start.";

        var courtIds = (body.CourtIds ?? []).Distinct().ToList();
        if (courtIds.Count is < 1 or > MaxCourts) return $"Choose 1 to {MaxCourts} courts.";
        var found = await db.Courts.CountAsync(c => courtIds.Contains(c.Id) && c.VenueId == venueId && c.Active && c.CategoryId == body.CategoryId, ct);
        if (found != courtIds.Count) return "Choose open courts of this sport at your venue.";
        return null;
    }

    private static void Apply(Tournament t, TournamentBody body, int teamSize)
    {
        t.CategoryId = body.CategoryId;
        t.NameEn = body.NameEn!.Trim();
        t.NameAr = body.NameAr!.Trim();
        t.DescriptionEn = Blank(body.DescriptionEn);
        t.DescriptionAr = Blank(body.DescriptionAr);
        t.Format = body.Format;
        t.TeamSize = teamSize;
        t.MaxTeams = body.MaxTeams;
        t.EntryFee = body.EntryFee;
        t.Level = body.Level;
        t.CourtIds = (body.CourtIds ?? []).Distinct().ToArray();
        t.StartsAt = body.StartsAt.ToUniversalTime();
        t.EndsAt = body.EndsAt.ToUniversalTime();
        t.RegistrationClosesAt = body.RegistrationClosesAt.ToUniversalTime();
    }

    /// A tournament with its venue, all its teams (members in position order), court names and captains.
    private sealed record Loaded(
        Tournament T, Venue Venue, List<TournamentTeam> Teams, List<TournamentMatch> Matches,
        Dictionary<Guid, CourtNameDto> CourtNames, Dictionary<Guid, (string? Name, string Phone)> Captains)
    {
        private readonly TimeZoneInfo zone = VenueClock.Zone(Venue);
        private readonly Dictionary<Guid, TournamentTeam> byId = Teams.ToDictionary(x => x.Id);

        public DateTimeOffset Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, zone);

        public int Count(TeamStatus status) => Teams.Count(x => x.Status == status);

        public List<CourtNameDto> Courts => T.CourtIds.Where(CourtNames.ContainsKey).Select(id => CourtNames[id]).ToList();

        /// Registered by time, then Waitlisted by time, then Withdrawn.
        public IEnumerable<TournamentTeam> Ordered => Teams.OrderBy(x => x.Status).ThenBy(x => x.RegisteredAt);

        public MatchDto Match(TournamentMatch m)
        {
            var court = m.CourtId is { } c ? CourtNames.GetValueOrDefault(c) : null;
            return new MatchDto(m.Id, m.Stage, m.Round, m.Position, m.GroupNo, Ref(m.TeamAId), Ref(m.TeamBId),
                m.ScoreA, m.ScoreB, m.WinnerTeamId, m.IsBye, m.CourtId, court?.NameEn, court?.NameAr,
                m.StartsAt is { } s ? Local(s) : null, m.NextMatchId, m.NextSlot);
        }

        private TeamRefDto? Ref(Guid? id) => id is { } x && byId.TryGetValue(x, out var team) ? new TeamRefDto(x, team.Name) : null;

        public List<MatchDto> MatchDtos => Matches.OrderBy(m => m.Stage).ThenBy(m => m.Round).ThenBy(m => m.Position).Select(Match).ToList();

        /// Round robin tables; empty for a knockout or before the draw.
        public List<GroupDto> Groups => Teams.Where(x => x.GroupNo != null).GroupBy(x => x.GroupNo!.Value).OrderBy(g => g.Key)
            .Select(g => new GroupDto(g.Key, TournamentDraw.Standings(
                g.Select(x => (x.Id, x.Name)).ToList(), ScoredResults(Matches.Where(m => m.GroupNo == g.Key))))).ToList();

        public bool OverrunsEnd => T.MatchMinutes is { } minutes && Matches.Any(m => m.StartsAt is { } s && s.AddMinutes(minutes) > T.EndsAt);

        public string? ChampionName => T.ChampionTeamId is { } c ? byId.GetValueOrDefault(c)?.Name : null;

        /// The team's earliest unplayed, scheduled match.
        public MatchDto? NextMatch(Guid teamId) => Matches
            .Where(m => !m.IsBye && m.ScoreA == null && m.StartsAt != null && (m.TeamAId == teamId || m.TeamBId == teamId))
            .OrderBy(m => m.StartsAt).Select(Match).FirstOrDefault();
    }

    // ponytail: loads every team of every tournament in the query. Fine for a venue's dozens; page it if lists grow.
    private static async Task<List<Loaded>> LoadAsync(MidangoDb db, IQueryable<Tournament> query, CancellationToken ct)
    {
        var rows = await (from t in query join v in db.Venues on t.VenueId equals v.Id select new { t, v }).AsNoTracking().ToListAsync(ct);
        var ids = rows.Select(r => r.t.Id).ToList();
        var teams = await db.TournamentTeams.AsNoTracking().Include(x => x.Members).Where(x => ids.Contains(x.TournamentId)).ToListAsync(ct);
        foreach (var team in teams) team.Members = team.Members.OrderBy(m => m.Position).ToList();
        var courtIds = rows.SelectMany(r => r.t.CourtIds).Distinct().ToList();
        var courts = await db.Courts.AsNoTracking().Where(c => courtIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new CourtNameDto(c.Id, c.NameEn, c.NameAr), ct);
        var captainIds = teams.Select(x => x.CaptainCustomerId).Distinct().ToList();
        var captains = await db.Customers.AsNoTracking().Where(c => captainIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => (c.Name, c.PhoneE164), ct);
        var matches = (await db.TournamentMatches.AsNoTracking().Where(m => ids.Contains(m.TournamentId)).ToListAsync(ct)).ToLookup(m => m.TournamentId);
        var byTournament = teams.ToLookup(x => x.TournamentId);
        return rows.Select(r => new Loaded(r.t, r.v, byTournament[r.t.Id].ToList(), matches[r.t.Id].ToList(), courts, captains)).ToList();
    }

    private static async Task<VenueTournamentDto?> VenueDetailAsync(MidangoDb db, ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var venueId = user.StaffVenueId()!.Value;
        if ((await LoadAsync(db, db.Tournaments.Where(t => t.Id == id && t.VenueId == venueId), ct)).SingleOrDefault() is not { } l) return null;
        var t = l.T;
        return new VenueTournamentDto(
            t.Id, t.CategoryId, t.NameEn, t.NameAr, t.DescriptionEn, t.DescriptionAr, t.Format, t.TeamSize, t.MaxTeams,
            t.EntryFee, t.Currency, t.Level, l.Local(t.StartsAt), l.Local(t.EndsAt), l.Local(t.RegistrationClosesAt),
            t.Status, l.Local(t.CreatedAt), t.PublishedAt is { } p ? l.Local(p) : null, t.CancelledAt is { } c ? l.Local(c) : null,
            l.Count(TeamStatus.Registered), l.Count(TeamStatus.Waitlisted), l.Courts,
            l.Ordered.Select(x =>
            {
                var captain = l.Captains.GetValueOrDefault(x.CaptainCustomerId);
                return new StaffTeamDto(x.Id, x.Name, x.Status, l.Local(x.RegisteredAt),
                    new CaptainDto(captain.Name, captain.Phone is null ? "" : PhoneNumber.Mask(captain.Phone)),
                    x.Members.Where(m => m.Position > 0).Select(m => m.Name).ToList(), x.CheckedInAt is { } at ? l.Local(at) : null, x.CashCollected,
                    x.DrawPosition, x.GroupNo);
            }).ToList(),
            t.MatchMinutes, t.GroupCount, t.DrawnAt is { } d ? l.Local(d) : null, t.ChampionTeamId, l.OverrunsEnd, l.MatchDtos, l.Groups);
    }

    private static bool RegistrationOpen(Tournament t, DateTimeOffset now) =>
        t.Status == TournamentStatus.Published && now < t.RegistrationClosesAt;

    private static TournamentCardDto Card(Loaded l, DateTimeOffset now)
    {
        var t = l.T;
        return new TournamentCardDto(
            t.Id, t.VenueId, l.Venue.NameEn, l.Venue.NameAr, l.Venue.LogoPath, t.CategoryId, t.NameEn, t.NameAr, t.Format, t.TeamSize,
            l.Local(t.StartsAt), l.Local(t.EndsAt), l.Local(t.RegistrationClosesAt), t.EntryFee, t.Currency, t.MaxTeams,
            l.Count(TeamStatus.Registered), l.Count(TeamStatus.Waitlisted), t.Level, RegistrationOpen(t, now), t.Status);
    }

    private static TournamentPageDto Page(Loaded l, DateTimeOffset now)
    {
        var t = l.T;
        return new TournamentPageDto(
            t.Id, t.VenueId, l.Venue.NameEn, l.Venue.NameAr, l.Venue.LogoPath, t.CategoryId, t.NameEn, t.NameAr, t.Format, t.TeamSize,
            l.Local(t.StartsAt), l.Local(t.EndsAt), l.Local(t.RegistrationClosesAt), t.EntryFee, t.Currency, t.MaxTeams,
            l.Count(TeamStatus.Registered), l.Count(TeamStatus.Waitlisted), t.Level, RegistrationOpen(t, now), t.Status,
            t.DescriptionEn, t.DescriptionAr, l.Venue.AddressEn, l.Venue.AddressAr, l.Courts,
            l.Ordered.Where(x => x.Status == TeamStatus.Registered)
                .Select(x => new PublicTeamDto(x.Name, x.Members.Select(m => MatchEndpoints.FirstName(m.Name)).ToList())).ToList(),
            l.MatchDtos, l.Groups, l.ChampionName);
    }

    private static MyTeamDto MyTeam(Loaded l, TournamentTeam team)
    {
        int? position = team.Status != TeamStatus.Waitlisted ? null
            : l.Teams.Where(x => x.Status == TeamStatus.Waitlisted).OrderBy(x => x.RegisteredAt).ToList().FindIndex(x => x.Id == team.Id) + 1;
        return new MyTeamDto(team.Id, team.Name, team.Status, team.Members.Select(m => m.Name).ToList(),
            team.CheckedInAt is { } at ? l.Local(at) : null, position, l.NextMatch(team.Id));
    }

    /// Published or cancelled tournaments at published venues; drafts are never public.
    private static async Task<TournamentPageDto?> PageAsync(MidangoDb db, Guid id, DateTimeOffset now, CancellationToken ct) =>
        (await LoadAsync(db, db.Tournaments.Where(t => t.Id == id && t.Status != TournamentStatus.Draft
                                                       && db.Venues.Any(v => v.Id == t.VenueId && v.Published)), ct))
        .Select(l => Page(l, now)).SingleOrDefault();

    private static async Task<TeamResultDto> TeamResultAsync(MidangoDb db, Guid id, Guid teamId, DateTimeOffset now, CancellationToken ct)
    {
        var l = (await LoadAsync(db, db.Tournaments.Where(t => t.Id == id), ct)).Single();
        return new TeamResultDto(MyTeam(l, l.Teams.Single(x => x.Id == teamId)), Page(l, now));
    }
}
