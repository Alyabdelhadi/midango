using Midango.Api.Bookings;

namespace Midango.Api.Tournaments;

public enum TournamentFormat
{
    Knockout,
    /// Round-robin groups (no knockout stage after them yet).
    RoundRobin,
}

public enum TournamentStatus
{
    Draft,
    /// Open in the app; its courts are blocked for [StartsAt, EndsAt).
    Published,
    /// The draw is made; results are being entered.
    InProgress,
    /// Every match has a result.
    Finished,
    Cancelled,
}

public enum TeamStatus
{
    Registered,
    Waitlisted,
    Withdrawn,
}

/// A venue's tournament. Teams sign up in the app and pay the entry fee at the desk.
public sealed class Tournament
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid VenueId { get; init; }
    public Guid CategoryId { get; set; }
    public required string NameEn { get; set; }
    public required string NameAr { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public TournamentFormat Format { get; set; }

    /// Players per team, captain included.
    public int TeamSize { get; set; }
    public int MaxTeams { get; set; }

    /// Per team, paid in cash at check-in.
    public decimal EntryFee { get; set; }
    public required string Currency { get; set; }

    /// UTC instants.
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset RegistrationClosesAt { get; set; }

    public Guid[] CourtIds { get; set; } = [];

    /// Null = any level.
    public PlayLevel? Level { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    /// Set by the draw (null = no draw yet).
    public int? MatchMinutes { get; set; }
    /// Round robin only.
    public int? GroupCount { get; set; }
    public DateTimeOffset? DrawnAt { get; set; }
    /// Knockout: the final's winner. Round robin: the group winner when there is one group.
    public Guid? ChampionTeamId { get; set; }
}

public sealed class TournamentTeam
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TournamentId { get; init; }
    public required string Name { get; set; }
    public Guid CaptainCustomerId { get; init; }
    public TeamStatus Status { get; set; }

    /// Waitlist order: promotions take the oldest first.
    public DateTimeOffset RegisteredAt { get; init; }
    public DateTimeOffset? WithdrawnAt { get; set; }
    public DateTimeOffset? CheckedInAt { get; set; }

    /// The entry fee taken at check-in. Entry fees earn no loyalty points in T1.
    public decimal? CashCollected { get; set; }

    /// 1-based place in the shuffled draw order; null when not in the current draw.
    public int? DrawPosition { get; set; }
    /// Round robin group, 1-based.
    public int? GroupNo { get; set; }

    /// Position order; 0 is the captain.
    public List<TournamentTeamMember> Members { get; set; } = [];
}

/// Position 0 = the captain (CustomerId set, Name = account name); the others are names the captain typed.
public sealed class TournamentTeamMember
{
    public Guid TeamId { get; init; }
    public int Position { get; init; }
    public required string Name { get; init; }
    public Guid? CustomerId { get; init; }
}

public enum MatchStage
{
    Knockout,
    Group,
}

/// One fixture of a drawn tournament. A knockout bye has one team, no court/time, and is already won.
public sealed class TournamentMatch
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TournamentId { get; init; }
    public MatchStage Stage { get; init; }
    /// 1-based. Knockout: 1 = first round. Group: the round of the circle schedule.
    public int Round { get; init; }
    /// 1-based within the round (across all groups for group matches).
    public int Position { get; init; }
    public int? GroupNo { get; init; }
    public Guid? TeamAId { get; set; }
    public Guid? TeamBId { get; set; }
    /// 0-99 each; both set or both null.
    public int? ScoreA { get; set; }
    public int? ScoreB { get; set; }
    /// Null for a group draw or no result yet.
    public Guid? WinnerTeamId { get; set; }
    public Guid? CourtId { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    /// Knockout: where the winner goes, and into which slot ("A" or "B").
    public Guid? NextMatchId { get; init; }
    public string? NextSlot { get; init; }
    public bool IsBye { get; init; }
    public DateTimeOffset? ResultAt { get; set; }
}
