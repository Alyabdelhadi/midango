using System.Security.Claims;
using Midango.Api.Data;

namespace Midango.Api.Identity;

public static class TokenEndpoints
{
    public sealed record RefreshBody(string? RefreshToken);

    public static void MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth");

        // Mobile sends the token in the body; the web sends an empty body and the cookie comes along.
        auth.MapPost("/refresh", async (
            RefreshBody body, HttpContext http, TokenService tokens, MidangoDb db, CancellationToken ct) =>
        {
            var fromCookie = string.IsNullOrEmpty(body.RefreshToken);
            var raw = fromCookie ? http.Request.Cookies[SessionCookie.Name] : body.RefreshToken;
            if (string.IsNullOrEmpty(raw)) return ApiErrors.Fail(401, "Please sign in again.");

            var spent = await tokens.SpendAsync(raw, ct);
            if (spent is null)
            {
                if (fromCookie) SessionCookie.Clear(http);
                return ApiErrors.Fail(401, "Please sign in again.");
            }

            // Claims are rebuilt from the database, so a role change or deactivation takes effect here.
            IReadOnlyList<Claim>? claims = spent.SubjectKind switch
            {
                SubjectKind.Customer => await db.Customers.FindAsync([spent.SubjectId], ct) is { } customer
                    ? AppClaims.For(customer)
                    : null,
                SubjectKind.Staff => await db.Staff.FindAsync([spent.SubjectId], ct) is { Active: true } staff
                    ? AppClaims.For(staff)
                    : null,
                _ => null,
            };
            if (claims is null)
            {
                if (fromCookie) SessionCookie.Clear(http);
                return ApiErrors.Fail(401, "Please sign in again.");
            }

            var pair = await tokens.IssueAsync(spent.SubjectKind, spent.SubjectId, claims, ct);
            if (!fromCookie) return Results.Ok(pair);

            SessionCookie.Write(http, pair);
            return Results.Ok(new SessionCookie.SessionResponse(pair.AccessToken, pair.AccessExpiresAt, null));
        });

        auth.MapPost("/logout", async (RefreshBody body, HttpContext http, TokenService tokens, CancellationToken ct) =>
        {
            var raw = string.IsNullOrEmpty(body.RefreshToken) ? http.Request.Cookies[SessionCookie.Name] : body.RefreshToken;
            if (!string.IsNullOrEmpty(raw)) await tokens.RevokeAsync(raw, ct);
            SessionCookie.Clear(http);
            return Results.NoContent();
        });
    }
}
