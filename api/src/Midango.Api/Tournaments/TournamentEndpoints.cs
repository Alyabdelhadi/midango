using System.Security.Claims;
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
        IReadOnlyList<string> Members, DateTimeOffset? CheckedInAt, decimal? CashCollected);

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
        int Registered, int Waitlisted, IReadOnlyList<CourtNameDto> Courts, IReadOnlyList<StaffTeamDto> Teams);

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
        IReadOnlyList<CourtNameDto> Courts, IReadOnlyList<PublicTeamDto> Teams);

    /// The captain's own team: full names, captain first. WaitlistPosition is 1-based, null unless waitlisted.
    public sealed record MyTeamDto(
        Guid Id, string Name, TeamStatus Status, IReadOnlyList<string> Members, DateTimeOffset? CheckedInAt, int? WaitlistPosition);

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
                    promoted = Promote(t, teams);
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
            if (t.Status == TournamentStatus.Published) return Results.Ok(await VenueDetailAsync(db, user, id, ct));
            if (t.Status == TournamentStatus.Cancelled) return ApiErrors.Fail(409, "This tournament was cancelled.");
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
                if (t.EndsAt <= now) return ApiErrors.Fail(409, "This tournament has already finished.");

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
                if (t.Status != TournamentStatus.Published) return "Only a published tournament can check teams in.";
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

        // Public, like /matches.
        app.MapGet("/tournaments", async (Guid? categoryId, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow();
            var loaded = await LoadAsync(db, db.Tournaments.Where(t =>
                t.Status == TournamentStatus.Published && t.EndsAt > now
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
        Tournament T, Venue Venue, List<TournamentTeam> Teams,
        Dictionary<Guid, CourtNameDto> CourtNames, Dictionary<Guid, (string? Name, string Phone)> Captains)
    {
        private readonly TimeZoneInfo zone = VenueClock.Zone(Venue);

        public DateTimeOffset Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, zone);

        public int Count(TeamStatus status) => Teams.Count(x => x.Status == status);

        public List<CourtNameDto> Courts => T.CourtIds.Where(CourtNames.ContainsKey).Select(id => CourtNames[id]).ToList();

        /// Registered by time, then Waitlisted by time, then Withdrawn.
        public IEnumerable<TournamentTeam> Ordered => Teams.OrderBy(x => x.Status).ThenBy(x => x.RegisteredAt);
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
        var byTournament = teams.ToLookup(x => x.TournamentId);
        return rows.Select(r => new Loaded(r.t, r.v, byTournament[r.t.Id].ToList(), courts, captains)).ToList();
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
                    x.Members.Where(m => m.Position > 0).Select(m => m.Name).ToList(), x.CheckedInAt is { } at ? l.Local(at) : null, x.CashCollected);
            }).ToList());
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
                .Select(x => new PublicTeamDto(x.Name, x.Members.Select(m => MatchEndpoints.FirstName(m.Name)).ToList())).ToList());
    }

    private static MyTeamDto MyTeam(Loaded l, TournamentTeam team)
    {
        int? position = team.Status != TeamStatus.Waitlisted ? null
            : l.Teams.Where(x => x.Status == TeamStatus.Waitlisted).OrderBy(x => x.RegisteredAt).ToList().FindIndex(x => x.Id == team.Id) + 1;
        return new MyTeamDto(team.Id, team.Name, team.Status, team.Members.Select(m => m.Name).ToList(),
            team.CheckedInAt is { } at ? l.Local(at) : null, position);
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
