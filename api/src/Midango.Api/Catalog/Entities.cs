namespace Midango.Api.Catalog;

/// A sport the platform supports. Created only by the platform operator.
public sealed class SportCategory
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string NameEn { get; set; }
    public required string NameAr { get; set; }

    /// Lucide icon name in kebab-case, e.g. "volleyball". Clients fall back to a default icon if unknown.
    public required string IconKey { get; set; }

    /// Bookable lengths in minutes, ascending, e.g. [60, 90, 120].
    public int[] DurationsMinutes { get; set; } = [];
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
    public List<CourtFeature> Features { get; set; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// A sport-specific court attribute that customers can filter by, e.g. "Panoramic".
public sealed class CourtFeature
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CategoryId { get; init; }
    public required string NameEn { get; set; }
    public required string NameAr { get; set; }
    public int SortOrder { get; set; }
}

public sealed class Court
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid VenueId { get; init; }
    public Guid CategoryId { get; set; }
    public required string NameEn { get; set; }
    public required string NameAr { get; set; }
    public bool Indoor { get; set; }

    /// Always features of this court's category.
    public List<CourtFeature> Features { get; set; } = [];
    public int SortOrder { get; set; }

    /// False = closed (not bookable); shown as such in Business.
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// One opening period for one day of the week. A day with no row is closed.
public sealed class OpeningHours
{
    public Guid VenueId { get; init; }
    public DayOfWeek Day { get; init; }
    public TimeOnly Opens { get; set; }

    /// Closes <= Opens means the venue closes after midnight (e.g. 16:00–02:00).
    public TimeOnly Closes { get; set; }
}
