using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Platform;

/// Owner tools: the Midango team (operators and their roles) and the audit log of operator changes.
public static class AdminToolsEndpoints
{
    public const int PageSize = 50;

    public sealed record TeamMemberDto(
        Guid Id, string Email, string DisplayName, OperatorRole Role, bool Active, bool MustChangePassword, DateTimeOffset CreatedAt);
    public sealed record NewTeamMemberBody(string? Email, string? DisplayName, OperatorRole Role, string? TemporaryPassword);
    public sealed record UpdateTeamMemberBody(OperatorRole? Role, bool? Active);
    public sealed record ResetBody(string? TemporaryPassword);

    public sealed record AuditDto(
        long Id, DateTimeOffset At, string OperatorName, string Method, string Path, int Status,
        Guid? VenueId, string? VenueNameEn, string? VenueNameAr);
    public sealed record AuditPage(IReadOnlyList<AuditDto> Items, int Total);

    private static TeamMemberDto Dto(StaffMember s) =>
        new(s.Id, s.Email, s.DisplayName, s.OperatorRole ?? OperatorRole.Owner, s.Active, s.MustChangePassword, s.CreatedAt);

    public static void MapAdminToolsEndpoints(this IEndpointRouteBuilder app)
    {
        var team = app.MapGroup("/admin/team").RequireAuthorization(Policies.PlatformOperator);

        team.MapGet("", async (MidangoDb db, CancellationToken ct) =>
            Results.Ok((await db.Staff.AsNoTracking()
                    .Where(s => s.Role == StaffRole.PlatformOperator)
                    .OrderBy(s => s.DisplayName)
                    .ToListAsync(ct))
                .Select(Dto)));

        team.MapPost("", async (NewTeamMemberBody body, MidangoDb db, IPasswordHasher<StaffMember> hasher, CancellationToken ct) =>
        {
            var email = StaffAccounts.NormaliseEmail(body.Email);
            if (email is null) return ApiErrors.Fail(400, "Enter a valid email address.");
            var name = body.DisplayName?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 80) return ApiErrors.Fail(400, "Enter a name of up to 80 characters.");
            if (body.TemporaryPassword is not { Length: >= StaffAccounts.MinPasswordLength } temp)
                return ApiErrors.Fail(400, $"Use a temporary password of at least {StaffAccounts.MinPasswordLength} characters.");
            if (await db.Staff.AnyAsync(s => s.Email == email, ct)) return ApiErrors.Fail(409, "That email already has an account.");

            var member = new StaffMember
            {
                Email = email, DisplayName = name, Role = StaffRole.PlatformOperator, OperatorRole = body.Role,
                MustChangePassword = true,
            };
            member.PasswordHash = hasher.HashPassword(member, temp);
            db.Staff.Add(member);
            await db.SaveChangesAsync(ct);
            return Results.Json(Dto(member), statusCode: 201);
        });

        team.MapPut("/{id:guid}", async (
            Guid id, UpdateTeamMemberBody body, ClaimsPrincipal user, MidangoDb db, TokenService tokens, CancellationToken ct) =>
        {
            if (id == user.SubjectId()) return ApiErrors.Fail(400, "You can't change your own role or access.");
            var member = await db.Staff.SingleOrDefaultAsync(s => s.Id == id && s.Role == StaffRole.PlatformOperator, ct);
            if (member is null) return ApiErrors.Fail(404, "Team member not found.");

            var losesOwner = member.OperatorRole == OperatorRole.Owner && member.Active
                             && (body.Active == false || (body.Role is { } r && r != OperatorRole.Owner));
            if (losesOwner && !await db.Staff.AnyAsync(s => s.Id != id && s.Role == StaffRole.PlatformOperator
                                                            && s.Active && s.OperatorRole == OperatorRole.Owner, ct))
                return ApiErrors.Fail(400, "Keep at least one active Owner.");

            if (body.Role is { } role) member.OperatorRole = role;
            if (body.Active is { } active) member.Active = active;
            await db.SaveChangesAsync(ct);
            // Sign them out everywhere so the new role (or the deactivation) applies right away.
            await tokens.RevokeAllAsync(SubjectKind.Staff, member.Id, ct);
            return Results.Ok(Dto(member));
        });

        team.MapPost("/{id:guid}/reset-password", async (
            Guid id, ResetBody body, ClaimsPrincipal user, MidangoDb db, IPasswordHasher<StaffMember> hasher,
            TokenService tokens, CancellationToken ct) =>
        {
            if (id == user.SubjectId()) return ApiErrors.Fail(400, "Change your own password from your account instead.");
            var member = await db.Staff.SingleOrDefaultAsync(s => s.Id == id && s.Role == StaffRole.PlatformOperator, ct);
            if (member is null) return ApiErrors.Fail(404, "Team member not found.");
            if (body.TemporaryPassword is not { Length: >= StaffAccounts.MinPasswordLength } temp)
                return ApiErrors.Fail(400, $"Use at least {StaffAccounts.MinPasswordLength} characters.");
            member.PasswordHash = hasher.HashPassword(member, temp);
            member.MustChangePassword = true;
            await db.SaveChangesAsync(ct);
            await tokens.RevokeAllAsync(SubjectKind.Staff, member.Id, ct);
            return Results.NoContent();
        });

        app.MapGet("/admin/audit", async (int? page, string? q, Guid? operatorId, MidangoDb db, CancellationToken ct) =>
        {
            var query = db.OperatorActions.AsNoTracking();
            if (operatorId is { } op) query = query.Where(a => a.OperatorId == op);
            if (q?.Trim() is { Length: > 0 } term)
            {
                var pattern = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                query = query.Where(a => EF.Functions.ILike(a.Path, pattern)
                                         || db.Staff.Any(s => s.Id == a.OperatorId && EF.Functions.ILike(s.DisplayName, pattern))
                                         || db.Venues.Any(v => v.Id == a.VenueId && EF.Functions.ILike(v.NameEn, pattern)));
            }
            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(a => a.At)
                .Skip((Math.Max(1, page ?? 1) - 1) * PageSize).Take(PageSize)
                .Select(a => new AuditDto(
                    a.Id, a.At,
                    db.Staff.Where(s => s.Id == a.OperatorId).Select(s => s.DisplayName).First(),
                    a.Method, a.Path, a.Status, a.VenueId,
                    db.Venues.Where(v => v.Id == a.VenueId).Select(v => v.NameEn).FirstOrDefault(),
                    db.Venues.Where(v => v.Id == a.VenueId).Select(v => v.NameAr).FirstOrDefault()))
                .ToListAsync(ct);
            return Results.Ok(new AuditPage(items, total));
        }).RequireAuthorization(Policies.PlatformOperator);
    }
}
