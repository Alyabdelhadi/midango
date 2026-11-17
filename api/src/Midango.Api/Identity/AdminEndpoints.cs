using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Venues;

namespace Midango.Api.Identity;

public static class AdminEndpoints
{
    public sealed record CreateVenueBody(string? NameEn, string? NameAr);
    public sealed record VenueSummary(Guid Id, string NameEn, string NameAr);
    public sealed record CreateVenueStaffBody(string? Email, string? DisplayName, StaffRole Role, string? TemporaryPassword);
    public sealed record CreateDeskStaffBody(string? Email, string? DisplayName, string? TemporaryPassword);

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin").RequireAuthorization(Policies.PlatformOperator);

        admin.MapGet("/venues", async (MidangoDb db, CancellationToken ct) =>
            Results.Ok(await db.Venues
                .OrderBy(v => v.NameEn)
                .Select(v => new VenueSummary(v.Id, v.NameEn, v.NameAr))
                .ToListAsync(ct)));

        admin.MapPost("/venues", async (CreateVenueBody body, MidangoDb db, CancellationToken ct) =>
        {
            var nameEn = body.NameEn?.Trim();
            var nameAr = body.NameAr?.Trim();
            // Both are required: the app is bilingual and a missing name shows as a blank card in Arabic.
            if (string.IsNullOrEmpty(nameEn) || string.IsNullOrEmpty(nameAr) || nameEn.Length > 120 || nameAr.Length > 120)
                return ApiErrors.Fail(400, "Enter the venue name in English and Arabic, up to 120 characters each.");

            var venue = new Venue { NameEn = nameEn, NameAr = nameAr };
            db.Venues.Add(venue);
            await db.SaveChangesAsync(ct);
            return Results.Json(new VenueSummary(venue.Id, venue.NameEn, venue.NameAr), statusCode: 201);
        });

        admin.MapPost("/venues/{venueId:guid}/staff", (
            Guid venueId, CreateVenueStaffBody body, MidangoDb db, IPasswordHasher<StaffMember> hasher, CancellationToken ct) =>
            StaffAccounts.CreateAsync(
                db, hasher, venueId, body.Role, body.Email, body.DisplayName, body.TemporaryPassword, ct));

        // A venue manager manages the desk staff of their own venue only.
        var venueStaff = app.MapGroup("/venue/staff").RequireAuthorization(Policies.VenueManager);

        venueStaff.MapGet("", async (ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId();
            return Results.Ok(await db.Staff
                .Where(s => s.VenueId == venueId)
                .OrderBy(s => s.DisplayName)
                .Select(s => new StaffSummary(s.Id, s.Email, s.DisplayName, s.Role, s.Active))
                .ToListAsync(ct));
        });

        venueStaff.MapPost("", (
            CreateDeskStaffBody body, ClaimsPrincipal user, MidangoDb db, IPasswordHasher<StaffMember> hasher, CancellationToken ct) =>
            StaffAccounts.CreateAsync(
                db, hasher, user.StaffVenueId()!.Value, StaffRole.DeskStaff,
                body.Email, body.DisplayName, body.TemporaryPassword, ct));
    }
}
