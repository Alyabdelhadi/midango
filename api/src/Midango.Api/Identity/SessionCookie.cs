namespace Midango.Api.Identity;

/// Web clients ask for "?session=cookie": the refresh token then travels only in an HttpOnly cookie,
/// so page scripts can never read it. Mobile clients keep receiving it in the body.
public static class SessionCookie
{
    public const string Name = "mg_refresh";

    public sealed record SessionResponse(string AccessToken, DateTimeOffset AccessExpiresAt, string? RefreshToken);

    public static bool Requested(HttpContext http) => http.Request.Query["session"] == "cookie";

    public static void Write(HttpContext http, TokenPair pair) =>
        http.Response.Cookies.Append(Name, pair.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow + TokenService.RefreshLifetime,
        });

    public static void Clear(HttpContext http) =>
        http.Response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });

    /// The refresh token for the response body: null when it went into the cookie instead.
    public static string? Deliver(HttpContext http, TokenPair pair)
    {
        if (!Requested(http)) return pair.RefreshToken;
        Write(http, pair);
        return null;
    }
}
