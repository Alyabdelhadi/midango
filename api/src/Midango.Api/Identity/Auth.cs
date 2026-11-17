using System.Security.Claims;

namespace Midango.Api.Identity;

public static class Policies
{
    public const string Customer = "customer";
    public const string Staff = "staff";
    public const string PlatformOperator = "platform-operator";
    public const string VenueManager = "venue-manager";
    public const string VenueStaff = "venue-staff";
    public const string AuthRateLimit = "auth";
}

public static class AppClaims
{
    public const string Subject = "sub";
    public const string Kind = "kind";
    public const string Role = "role";
    public const string VenueId = "venue_id";

    public static IReadOnlyList<Claim> For(Customer customer) =>
    [
        new(Subject, customer.Id.ToString()),
        new(Kind, "customer"),
    ];

    public static IReadOnlyList<Claim> For(StaffMember staff)
    {
        List<Claim> claims =
        [
            new(Subject, staff.Id.ToString()),
            new(Kind, "staff"),
            new(Role, staff.Role.ToString()),
        ];
        if (staff.VenueId is { } venueId) claims.Add(new(VenueId, venueId.ToString()));
        return claims;
    }

    public static Guid SubjectId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(Subject) ?? throw new InvalidOperationException("Token has no subject."));

    public static Guid? StaffVenueId(this ClaimsPrincipal user) =>
        user.FindFirstValue(VenueId) is { } value ? Guid.Parse(value) : null;
}

public static class ApiErrors
{
    /// Problem details whose title is a sentence the person on the other end can act on.
    public static IResult Fail(int status, string title) => Results.Problem(statusCode: status, title: title);
}
