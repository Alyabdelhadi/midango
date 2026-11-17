using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;

namespace Midango.Api.Identity;

public sealed record StaffSummary(Guid Id, string Email, string DisplayName, StaffRole Role, bool Active);

public static class StaffAccounts
{
    public const int MinPasswordLength = 10;

    // Verified against when the email is unknown, so a wrong email takes as long as a wrong password.
    private static readonly string DummyHash =
        new PasswordHasher<StaffMember>().HashPassword(null!, "not-a-real-password-0000");

    public static string? NormaliseEmail(string? email) =>
        MailAddress.TryCreate(email?.Trim(), out var address) ? address.Address.ToLowerInvariant() : null;

    /// True when the password matches. Upgrades the stored hash if the hasher asks for it (the caller saves).
    public static bool CheckPassword(IPasswordHasher<StaffMember> hasher, StaffMember? staff, string? password)
    {
        var result = hasher.VerifyHashedPassword(staff!, staff?.PasswordHash ?? DummyHash, password ?? "");
        if (staff is null || result == PasswordVerificationResult.Failed) return false;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            staff.PasswordHash = hasher.HashPassword(staff, password!);
        return true;
    }

    public static async Task<IResult> CreateAsync(
        MidangoDb db, IPasswordHasher<StaffMember> hasher, Guid venueId, StaffRole role,
        string? email, string? displayName, string? temporaryPassword, CancellationToken ct)
    {
        if (role == StaffRole.PlatformOperator)
            return ApiErrors.Fail(400, "Choose a role: VenueManager or DeskStaff.");
        var normalised = NormaliseEmail(email);
        if (normalised is null) return ApiErrors.Fail(400, "Enter a valid email address.");
        var name = displayName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 80)
            return ApiErrors.Fail(400, "Enter a name of up to 80 characters.");
        if (temporaryPassword is not { Length: >= MinPasswordLength })
            return ApiErrors.Fail(400, $"The temporary password needs at least {MinPasswordLength} characters.");
        if (!await db.Venues.AnyAsync(v => v.Id == venueId, ct))
            return ApiErrors.Fail(404, "Venue not found.");
        if (await db.Staff.AnyAsync(s => s.Email == normalised, ct))
            return ApiErrors.Fail(409, "This email already has an account.");

        var staff = new StaffMember
        {
            Email = normalised,
            DisplayName = name,
            Role = role,
            VenueId = venueId,
            MustChangePassword = true,
        };
        staff.PasswordHash = hasher.HashPassword(staff, temporaryPassword);
        db.Staff.Add(staff);
        await db.SaveChangesAsync(ct);

        return Results.Json(
            new StaffSummary(staff.Id, staff.Email, staff.DisplayName, staff.Role, staff.Active), statusCode: 201);
    }
}
