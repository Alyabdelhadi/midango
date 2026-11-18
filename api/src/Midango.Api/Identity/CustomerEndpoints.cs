using System.Diagnostics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;

namespace Midango.Api.Identity;

public static class CustomerEndpoints
{
    public sealed record OtpRequestBody(string? Phone);
    public sealed record OtpVerifyBody(string? Phone, string? Code);
    public sealed record SignInResponse(
        string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken, bool ProfileComplete);
    public sealed record MeResponse(Guid Id, string Phone, string? Name);
    public sealed record UpdateMeBody(string? Name);

    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var otp = app.MapGroup("/auth/otp").RequireRateLimiting(Policies.AuthRateLimit);

        otp.MapPost("/request", async (OtpRequestBody body, OtpService otpService, MidangoDb db, CancellationToken ct) =>
        {
            if (!PhoneNumber.TryParse(body.Phone, out var phone))
                return ApiErrors.Fail(400, "Enter a valid mobile number.");
            if (await db.Customers.AnyAsync(c => c.PhoneE164 == phone.E164 && c.BlockedAt != null, ct))
                return ApiErrors.Fail(403, Customers.BlockedMessage);

            return await otpService.RequestAsync(phone, ct) switch
            {
                OtpRequestResult.Sent => Results.Accepted(),
                OtpRequestResult.TooSoon => ApiErrors.Fail(429, "Wait a minute before asking for another code."),
                OtpRequestResult.TooMany => ApiErrors.Fail(429, "Too many codes requested. Try again in an hour."),
                OtpRequestResult.SenderUnavailable => ApiErrors.Fail(503, "Text messages are not available right now."),
                _ => throw new UnreachableException(),
            };
        });

        otp.MapPost("/verify", async (
            OtpVerifyBody body, OtpService otpService, TokenService tokens, MidangoDb db, CancellationToken ct) =>
        {
            if (!PhoneNumber.TryParse(body.Phone, out var phone))
                return ApiErrors.Fail(400, "Enter a valid mobile number.");
            if (body.Code is not { Length: 6 } code || !code.All(char.IsAsciiDigit))
                return ApiErrors.Fail(400, "Enter the 6-digit code.");
            if (!await otpService.VerifyAsync(phone, code, ct))
                return ApiErrors.Fail(401, "That code is wrong or has expired.");

            var customer = await db.Customers.SingleOrDefaultAsync(c => c.PhoneE164 == phone.E164, ct);
            if (customer?.BlockedAt is not null) return ApiErrors.Fail(403, Customers.BlockedMessage);
            if (customer is null)
            {
                customer = new Customer { PhoneE164 = phone.E164 };
                db.Customers.Add(customer);
                await db.SaveChangesAsync(ct);
            }

            var pair = await tokens.IssueAsync(SubjectKind.Customer, customer.Id, AppClaims.For(customer), ct);
            return Results.Ok(new SignInResponse(
                pair.AccessToken, pair.AccessExpiresAt, pair.RefreshToken, customer.Name is not null));
        });

        var me = app.MapGroup("/me").RequireAuthorization(Policies.Customer);

        me.MapGet("", async (ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
            await db.Customers.FindAsync([user.SubjectId()], ct) is { } customer
                ? Results.Ok(new MeResponse(customer.Id, customer.PhoneE164, customer.Name))
                : ApiErrors.Fail(404, "Account not found."));

        me.MapPut("", async (UpdateMeBody body, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var name = body.Name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 60)
                return ApiErrors.Fail(400, "Enter a name of up to 60 characters.");

            var customer = await db.Customers.FindAsync([user.SubjectId()], ct);
            if (customer is null) return ApiErrors.Fail(404, "Account not found.");
            customer.Name = name;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new MeResponse(customer.Id, customer.PhoneE164, customer.Name));
        });
    }
}
