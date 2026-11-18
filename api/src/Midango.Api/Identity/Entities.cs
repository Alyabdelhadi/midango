namespace Midango.Api.Identity;

public sealed class Customer
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// E.164, e.g. +966551234567. Only the customer ever sees it in full.
    public required string PhoneE164 { get; init; }
    public string? Name { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// Set by the operator. Stops sign-in and every write the customer can make (Customers.BlockedAsync).
    public DateTimeOffset? BlockedAt { get; set; }
    public string? BlockedReason { get; set; }
    public Guid? BlockedByStaffId { get; set; }

    /// No-shows at or before this instant don't count toward the no-show limit (history is kept).
    public DateTimeOffset? NoShowsClearedAt { get; set; }
}

public enum StaffRole
{
    PlatformOperator,
    VenueManager,
    DeskStaff,
}

public sealed class StaffMember
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// Always lower-case (see StaffAccounts.NormaliseEmail).
    public required string Email { get; init; }
    public required string DisplayName { get; set; }
    public string PasswordHash { get; set; } = "";
    public StaffRole Role { get; init; }

    /// Null only for PlatformOperator, enforced by a database check constraint.
    public Guid? VenueId { get; init; }
    public bool Active { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class OtpChallenge
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string PhoneE164 { get; init; }

    /// HMAC-SHA256 of "{Id}:{code}", hex. The code itself is never stored.
    public required string CodeHash { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public int Attempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public enum SubjectKind
{
    Customer,
    Staff,
}

public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// SHA-256 of the raw token, hex. The raw token only ever exists on the client.
    public required string TokenHash { get; init; }
    public SubjectKind SubjectKind { get; init; }
    public Guid SubjectId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
}
