using System.Security.Cryptography;

namespace Midango.Api.Bookings;

public enum BookingStatus
{
    Confirmed,
    Cancelled,
}

public sealed class Booking
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// Short code read out at the desk, e.g. "K7Q2MX".
    public required string Reference { get; set; }
    public Guid VenueId { get; init; }
    public Guid CourtId { get; init; }
    public Guid CustomerId { get; init; }

    /// UTC instants. Npgsql only writes offset-0 values to timestamptz.
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public int Minutes { get; init; }

    /// Price snapshot at booking time, VAT-inclusive. Later price changes never touch it.
    public decimal Total { get; init; }
    public decimal Net { get; init; }
    public decimal Vat { get; init; }
    public required string Currency { get; init; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTimeOffset CreatedAt { get; init; }

    /// Cancelling after this instant is allowed but marked late.
    public DateTimeOffset FreeCancellationUntil { get; init; }
    public DateTimeOffset? CancelledAt { get; set; }
    public bool LateCancellation { get; set; }
}

public static class BookingReference
{
    // No 0/O or 1/I/L: the code is read aloud and typed at the desk.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string New() => string.Create(6, 0, static (span, _) =>
    {
        for (var i = 0; i < span.Length; i++) span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
    });
}
