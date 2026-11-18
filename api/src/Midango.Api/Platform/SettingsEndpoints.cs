using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Platform;

public static class SettingsEndpoints
{
    public sealed record SettingsDto(int ReminderHoursBefore, int NoShowLimit, int NoShowWindowDays, int LimitedUpcomingBookings)
    {
        public static SettingsDto From(PlatformSettings s) =>
            new(s.ReminderHoursBefore, s.NoShowLimit, s.NoShowWindowDays, s.LimitedUpcomingBookings);
    }

    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin/settings").RequireAuthorization(Policies.PlatformOperator);

        admin.MapGet("", async (MidangoDb db, CancellationToken ct) =>
            Results.Ok(SettingsDto.From(await db.PlatformSettings.AsNoTracking().SingleAsync(ct))));

        admin.MapPut("", async (SettingsDto input, MidangoDb db, CancellationToken ct) =>
        {
            if (input.ReminderHoursBefore is < 1 or > 24) return ApiErrors.Fail(400, "Send the reminder between 1 and 24 hours before the start.");
            if (input.NoShowLimit is < 1 or > 10) return ApiErrors.Fail(400, "The no-show limit must be between 1 and 10.");
            if (input.NoShowWindowDays is < 7 or > 180) return ApiErrors.Fail(400, "Count no-shows over 7 to 180 days.");
            if (input.LimitedUpcomingBookings is < 0 or > 5) return ApiErrors.Fail(400, "Allow between 0 and 5 upcoming bookings while limited.");

            var settings = await db.PlatformSettings.SingleAsync(ct);
            settings.ReminderHoursBefore = input.ReminderHoursBefore;
            settings.NoShowLimit = input.NoShowLimit;
            settings.NoShowWindowDays = input.NoShowWindowDays;
            settings.LimitedUpcomingBookings = input.LimitedUpcomingBookings;
            await db.SaveChangesAsync(ct);
            return Results.Ok(SettingsDto.From(settings));
        });
    }
}
