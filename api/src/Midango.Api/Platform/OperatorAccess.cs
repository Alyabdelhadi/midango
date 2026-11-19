using System.Security.Claims;
using Midango.Api.Identity;

namespace Midango.Api.Platform;

/// Which admin areas each operator role may use. One table for the whole /admin API; the admin web app
/// mirrors it (layout/nav.ts) so people only see what they can open.
public static class OperatorAccess
{
    private static readonly OperatorRole[] Everyone = [OperatorRole.Owner, OperatorRole.Finance, OperatorRole.Support, OperatorRole.Content];
    private static readonly OperatorRole[] Finance = [OperatorRole.Owner, OperatorRole.Finance];
    private static readonly OperatorRole[] Support = [OperatorRole.Owner, OperatorRole.Support];
    private static readonly OperatorRole[] Content = [OperatorRole.Owner, OperatorRole.Content];
    private static readonly OperatorRole[] OwnerOnly = [OperatorRole.Owner];

    /// Most specific prefix first. Anything not listed (team, audit, errors, platform rules) is Owner-only.
    private static readonly (string Prefix, OperatorRole[] Roles)[] Areas =
    [
        ("/admin/overview", Everyone),
        ("/admin/settings/billing", Finance),
        ("/admin/accounting", Finance),
        ("/admin/categories", Support),
        ("/admin/features", Support),
        ("/admin/venues", Support),
        ("/admin/customers", Support),
        ("/admin/leads", Support),
        ("/admin/app-content", Content),
        ("/admin/broadcasts", Content),
        ("/admin/site-content", Content),
        ("/admin/legal", Content),
    ];

    public static OperatorRole[] RolesFor(string path, string method)
    {
        // Every operator may read the venue list: search and accounting show venue names.
        if (HttpMethods.IsGet(method) && path.Equals("/admin/venues", StringComparison.OrdinalIgnoreCase)) return Everyone;
        foreach (var (prefix, roles) in Areas)
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return roles;
        return OwnerOnly;
    }

    /// The operator's role; a token from before roles existed counts as Owner (every operator then was one).
    public static OperatorRole RoleOf(ClaimsPrincipal user) =>
        Enum.TryParse<OperatorRole>(user.FindFirstValue(AppClaims.OpRole), out var role) ? role : OperatorRole.Owner;

    /// Runs after authentication: a signed-in operator outside their role's areas gets 403.
    public static IApplicationBuilder UseOperatorAccess(this IApplicationBuilder app) =>
        app.Use(async (http, next) =>
        {
            var path = http.Request.Path.Value ?? "";
            if (path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
                && http.User.FindFirstValue(AppClaims.Role) == nameof(StaffRole.PlatformOperator)
                && !RolesFor(path, http.Request.Method).Contains(RoleOf(http.User)))
            {
                await ApiErrors.Fail(403, "Your role can't open this part of the admin.").ExecuteAsync(http);
                return;
            }
            await next();
        });
}
