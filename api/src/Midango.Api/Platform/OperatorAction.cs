namespace Midango.Api.Platform;

/// One change a platform operator made: inside a venue (VenueId set) or in the admin itself (VenueId null).
public sealed class OperatorAction
{
    public long Id { get; init; }
    public Guid OperatorId { get; init; }
    public Guid? VenueId { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public int Status { get; init; }
    public DateTimeOffset At { get; init; }
}
