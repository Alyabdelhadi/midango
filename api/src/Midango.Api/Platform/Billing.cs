namespace Midango.Api.Platform;

/// How a venue pays Midango. Default = the platform's commission percent (PlatformSettings.CommissionPercent).
public enum FeeMode
{
    Default,
    Percent,
    Fixed,
}

/// A month marked as settled for one venue. No row = not paid yet. Amount is the net that was due when marked.
public sealed class VenueStatement
{
    public Guid VenueId { get; init; }

    /// First day of the month (UTC calendar month).
    public DateOnly Month { get; init; }
    public decimal Amount { get; init; }
    public required string Currency { get; init; }
    public DateTimeOffset PaidAt { get; init; }
    public Guid PaidByStaffId { get; init; }
}

/// An unexpected server error (HTTP 500), kept 30 days for the Error log page.
public sealed class ErrorEntry
{
    public long Id { get; init; }
    public DateTimeOffset At { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public required string ExceptionType { get; init; }
    public required string Message { get; init; }
    public required string Detail { get; init; }
    public Guid? StaffId { get; init; }
    public required string TraceId { get; init; }
}
