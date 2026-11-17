using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;

namespace Midango.Api.Identity;

public static class StaffEndpoints
{
    public sealed record LoginBody(string? Email, string? Password);
    public sealed record StaffSignInResponse(
        string AccessToken, DateTimeOffset AccessExpiresAt, string? RefreshToken, bool MustChangePassword);
    public sealed record StaffMeResponse(
        Guid Id, string Email, string DisplayName, StaffRole Role, Guid? VenueId, bool MustChangePassword);
    public sealed record ChangePasswordBody(string? CurrentPassword, string? NewPassword);

    public static void MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/staff/auth/login", async (
            HttpContext http, LoginBody body, MidangoDb db, IPasswordHasher<StaffMember> hasher, TokenService tokens, CancellationToken ct) =>
        {
            var email = StaffAccounts.NormaliseEmail(body.Email);
            var staff = email is null ? null : await db.Staff.SingleOrDefaultAsync(s => s.Email == email && s.Active, ct);
            if (!StaffAccounts.CheckPassword(hasher, staff, body.Password))
                return ApiErrors.Fail(401, "Email or password is wrong.");

            var pair = await tokens.IssueAsync(SubjectKind.Staff, staff!.Id, AppClaims.For(staff), ct);
            return Results.Ok(new StaffSignInResponse(
                pair.AccessToken, pair.AccessExpiresAt, SessionCookie.Deliver(http, pair), staff.MustChangePassword));
        }).RequireRateLimiting(Policies.AuthRateLimit);

        var me = app.MapGroup("/staff/me").RequireAuthorization(Policies.Staff);

        me.MapGet("", async (ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var id = user.SubjectId();
            var staff = await db.Staff.SingleOrDefaultAsync(s => s.Id == id && s.Active, ct);
            return staff is null
                ? ApiErrors.Fail(404, "Account not found.")
                : Results.Ok(new StaffMeResponse(
                    staff.Id, staff.Email, staff.DisplayName, staff.Role, staff.VenueId, staff.MustChangePassword));
        });

        me.MapPost("/password", async (
            HttpContext http, ChangePasswordBody body, ClaimsPrincipal user, MidangoDb db,
            IPasswordHasher<StaffMember> hasher, TokenService tokens, CancellationToken ct) =>
        {
            var id = user.SubjectId();
            var staff = await db.Staff.SingleOrDefaultAsync(s => s.Id == id && s.Active, ct);
            if (staff is null || !StaffAccounts.CheckPassword(hasher, staff, body.CurrentPassword))
                return ApiErrors.Fail(400, "Your current password is wrong.");
            if (body.NewPassword is not { Length: >= StaffAccounts.MinPasswordLength } newPassword)
                return ApiErrors.Fail(400, $"Use at least {StaffAccounts.MinPasswordLength} characters.");

            staff.PasswordHash = hasher.HashPassword(staff, newPassword);
            staff.MustChangePassword = false;
            await db.SaveChangesAsync(ct);

            // Sign out every other device, then give this one a fresh session.
            await tokens.RevokeAllAsync(SubjectKind.Staff, staff.Id, ct);
            var pair = await tokens.IssueAsync(SubjectKind.Staff, staff.Id, AppClaims.For(staff), ct);
            return Results.Ok(new StaffSignInResponse(
                pair.AccessToken, pair.AccessExpiresAt, SessionCookie.Deliver(http, pair), false));
        });
    }
}
