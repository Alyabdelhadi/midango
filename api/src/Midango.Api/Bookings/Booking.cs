using System.Security.Cryptography;

namespace Midango.Api.Bookings;

public enum BookingStatus
{
    Confirmed,
    Cancelled,
    /// Didn't turn up. Not Confirmed, so the court is bookable again.
    NoShow,
}

public enum BookingKind
{
    Booking,
    /// Time taken off sale by the venue (maintenance, private event). No customer, no price.
    Block,
}

public enum BookingSource
{
    App,
    Staff,
}

public sealed class Booking
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// Short code read out at the desk, e.g. "K7Q2MX".
    public required string Reference { get; set; }
    public Guid VenueId { get; init; }
    public Guid CourtId { get; set; }

    /// Null for walk-ins without a phone and for blocks.
    public Guid? CustomerId { get; init; }

    /// The name staff typed at the desk; set on every staff booking, with or without an account.
    /// Staff see this rather than the account's own name, so typing a phone number never reveals who owns it.
    public string? GuestName { get; init; }

    public BookingKind Kind { get; init; } = BookingKind.Booking;
    public BookingSource Source { get; init; } = BookingSource.App;
    public Guid? CreatedByStaffId { get; init; }

    /// Staff note (walk-in detail, block reason). Never shown to the customer.
    public string? Note { get; set; }

    /// UTC instants. Npgsql only writes offset-0 values to timestamptz.
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int Minutes { get; init; }

    /// Price snapshot at booking time, VAT-inclusive. Later price changes never touch it.
    public decimal Total { get; init; }
    public decimal Net { get; init; }
    public decimal Vat { get; init; }
    public required string Currency { get; init; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTimeOffset CreatedAt { get; init; }

    /// Cancelling after this instant is allowed but marked late.
    public DateTimeOffset FreeCancellationUntil { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public bool LateCancellation { get; set; }

    /// Cancelled by the venue; never counts against the customer.
    public bool CancelledByVenue { get; set; }

    /// Set by "Take cash & check in". A checked-in booking always stays Confirmed (database check).
    public DateTimeOffset? CheckedInAt { get; set; }
    public Guid? CheckedInByStaffId { get; set; }

    /// Cash taken at check-in: the full total (no partial payments yet).
    public decimal? CashCollected { get; set; }

    public DateTimeOffset? NoShowAt { get; set; }

    /// When the "Still coming?" reminder was handled (sent, or skipped: no device, or booked too late for one).
    public DateTimeOffset? ReminderSentAt { get; set; }
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
