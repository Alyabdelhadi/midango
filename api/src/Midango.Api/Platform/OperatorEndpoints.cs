using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;
using static Midango.Api.Identity.AdminEndpoints;

namespace Midango.Api.Platform;

/// The operator's hands on a venue: open it (act as its manager), suspend it, and see what was changed.
public static class OperatorEndpoints
{
    public sealed record ActResponse(string AccessToken, DateTimeOffset ExpiresAt, Guid VenueId, string NameEn, string NameAr);
    public sealed record OperatorActionDto(DateTimeOffset At, string OperatorName, string Method, string Path, int Status);

    /// Records every change made with an operator's acting token, after the endpoint has answered.
    public static IApplicationBuilder UseOperatorActionLog(this IApplicationBuilder app) =>
        app.Use(async (http, next) =>
        {
            await next();
            if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) || HttpMethods.IsOptions(http.Request.Method)) return;
            if (http.User.ActingOperatorId() is not { } op || http.User.StaffVenueId() is not { } venue) return;
            var db = http.RequestServices.GetRequiredService<MidangoDb>();
            db.ChangeTracker.Clear(); // don't re-save anything the endpoint left tracked
            var path = http.Request.Path.Value ?? "";
            db.OperatorActions.Add(new OperatorAction
            {
                OperatorId = op, VenueId = venue, Method = http.Request.Method,
                Path = path[..Math.Min(300, path.Length)],
                Status = http.Response.StatusCode,
                At = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(),
            });
            await db.SaveChangesAsync(CancellationToken.None);
        });

    public static void MapOperatorEndpoints(this IEndpointRouteBuilder app)
    {
        // Acting tokens carry role VenueManager, so they can never reach these.
        var venues = app.MapGroup("/admin/venues").RequireAuthorization(Policies.PlatformOperator);

        venues.MapPost("/{id:guid}/act", async (Guid id, ClaimsPrincipal user, MidangoDb db, TokenService tokens, CancellationToken ct) =>
        {
            var venue = await db.Venues.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (venue is null) return ApiErrors.Fail(404, "Venue not found.");
            if (venue.Suspended) return ApiErrors.Fail(409, "This venue is suspended. Unsuspend it before opening it.");

            var operatorId = user.SubjectId();
            var op = await db.Staff.SingleAsync(s => s.Id == operatorId, ct);
            var (token, expiresAt) = tokens.CreateAccess(AppClaims.ActingFor(op, id), TokenService.ActingLifetime);
            return Results.Ok(new ActResponse(token, expiresAt, venue.Id, venue.NameEn, venue.NameAr));
        });

        venues.MapPost("/{id:guid}/suspend", async (Guid id, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var venue = await db.Venues.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (venue is null) return ApiErrors.Fail(404, "Venue not found.");
            venue.Suspended = true;
            venue.Published = false;
            await db.SaveChangesAsync(ct);

            var now = clock.GetUtcNow();
            await db.RefreshTokens
                .Where(t => t.SubjectKind == SubjectKind.Staff && t.RevokedAt == null
                    && db.Staff.Any(s => s.Id == t.SubjectId && s.VenueId == id))
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            return Results.Ok(new VenueSummary(venue.Id, venue.NameEn, venue.NameAr, venue.Published, venue.Suspended));
        });

        venues.MapPost("/{id:guid}/unsuspend", async (Guid id, MidangoDb db, CancellationToken ct) =>
        {
            var venue = await db.Venues.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (venue is null) return ApiErrors.Fail(404, "Venue not found.");
            venue.Suspended = false; // stays unpublished until someone publishes it
            await db.SaveChangesAsync(ct);
            return Results.Ok(new VenueSummary(venue.Id, venue.NameEn, venue.NameAr, venue.Published, venue.Suspended));
        });

        venues.MapGet("/{id:guid}/actions", async (Guid id, MidangoDb db, CancellationToken ct) =>
            Results.Ok(await db.OperatorActions.AsNoTracking()
                .Where(a => a.VenueId == id)
                .Join(db.Staff, a => a.OperatorId, s => s.Id, (a, s) => new { a, s.DisplayName })
                .OrderByDescending(x => x.a.At).ThenByDescending(x => x.a.Id)
                .Take(50)
                .Select(x => new OperatorActionDto(x.a.At, x.DisplayName, x.a.Method, x.a.Path, x.a.Status))
                .ToListAsync(ct)));
    }
}
