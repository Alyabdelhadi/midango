namespace Midango.Api.Platform;

/// App-wide switches Midango controls: force update, store links and maintenance.
/// Exactly one row (Id = 1), seeded by the migration. Banners are AppBanner rows; featured venues are Venue.FeaturedRank.
public sealed class AppContent
{
    public const int SingletonId = 1;
    public int Id { get; init; } = SingletonId;

    /// An app whose build number is lower sees the blocking "update" screen.
    public int MinAndroidBuild { get; set; } = 1;
    public int MinIosBuild { get; set; } = 1;
    public string? AndroidStoreUrl { get; set; }
    public string? IosStoreUrl { get; set; }

    /// App-side only: the app shows a blocking screen; the API keeps working.
    public bool Maintenance { get; set; }
    public string? MaintenanceMessageEn { get; set; }
    public string? MaintenanceMessageAr { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// One slide of the home banner slider, shown by SortOrder when Active.
public sealed class AppBanner
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string TitleEn { get; set; }
    public required string TitleAr { get; set; }
    public string? BodyEn { get; set; }
    public string? BodyAr { get; set; }

    /// Public "/media/…" path, or null.
    public string? ImagePath { get; set; }

    /// "None", "Venue", "Tournament" or "Url".
    public string LinkType { get; set; } = "None";

    /// Venue id, tournament id or https URL, depending on LinkType.
    public string? LinkTarget { get; set; }
    public bool Active { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// One "message all users" push, kept as history.
public sealed class Broadcast
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string TitleEn { get; init; }
    public required string TitleAr { get; init; }
    public required string BodyEn { get; init; }
    public required string BodyAr { get; init; }
    public DateTimeOffset SentAt { get; init; }
    public Guid SentByStaffId { get; init; }

    /// Tokens tried, and tried minus the ones FCM said are dead.
    public int Devices { get; init; }
    public int Delivered { get; init; }
}
