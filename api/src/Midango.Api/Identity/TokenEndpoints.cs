using System.Security.Claims;
using Midango.Api.Data;

namespace Midango.Api.Identity;

public static class TokenEndpoints
{
    public sealed record RefreshBody(string? RefreshToken);

    public static void MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth");

        auth.MapPost("/refresh", async (RefreshBody body, TokenService tokens, MidangoDb db, CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(body.RefreshToken)) return ApiErrors.Fail(401, "Please sign in again.");
            var spent = await tokens.SpendAsync(body.RefreshToken, ct);
            if (spent is null) return ApiErrors.Fail(401, "Please sign in again.");

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
            if (claims is null) return ApiErrors.Fail(401, "Please sign in again.");

            return Results.Ok(await tokens.IssueAsync(spent.SubjectKind, spent.SubjectId, claims, ct));
        });

        auth.MapPost("/logout", async (RefreshBody body, TokenService tokens, CancellationToken ct) =>
        {
            if (!string.IsNullOrEmpty(body.RefreshToken)) await tokens.RevokeAsync(body.RefreshToken, ct);
            return Results.NoContent();
        });
    }
}
