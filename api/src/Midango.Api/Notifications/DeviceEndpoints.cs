using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Notifications;

public static class DeviceEndpoints
{
    public sealed record DeviceBody(string? Token, string? Platform, string? Language);

    private static readonly string[] Platforms = ["android", "ios", "web"];

    public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/me/devices").RequireAuthorization(Policies.Customer);

        me.MapPut("", async (DeviceBody body, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var token = body.Token?.Trim();
            if (string.IsNullOrEmpty(token) || token.Length > 512) return ApiErrors.Fail(400, "Missing device token.");
            if (body.Platform is not { } platform || !Platforms.Contains(platform)) return ApiErrors.Fail(400, "Unknown platform.");
            var language = body.Language == "ar" ? "ar" : "en";

            var now = clock.GetUtcNow();
            var device = await db.CustomerDevices.SingleOrDefaultAsync(d => d.Token == token, ct);
            if (device is null)
            {
                db.CustomerDevices.Add(new CustomerDevice
                {
                    CustomerId = user.SubjectId(), Token = token, Platform = platform, Language = language,
                    CreatedAt = now, LastSeenAt = now,
                });
            }
            else
            {
                device.CustomerId = user.SubjectId(); // a phone that changed hands follows its new owner
                device.Platform = platform;
                device.Language = language;
                device.LastSeenAt = now;
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        me.MapDelete("", async (string? token, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var customerId = user.SubjectId();
            await db.CustomerDevices.Where(d => d.Token == token && d.CustomerId == customerId).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
    }
}
