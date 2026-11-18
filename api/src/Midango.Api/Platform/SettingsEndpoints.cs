using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Platform;

public static class SettingsEndpoints
{
    public sealed record SettingsDto(
        int ReminderHoursBefore, int NoShowLimit, int NoShowWindowDays, int LimitedUpcomingBookings,
        decimal PointsPerSar, int SilverGames, int GoldGames, decimal SilverMultiplier, decimal GoldMultiplier,
        int TierWindowDays, int PointsPerRedeemStep, decimal SarPerRedeemStep, int MaxRedeemPercent)
    {
        public static SettingsDto From(PlatformSettings s) =>
            new(s.ReminderHoursBefore, s.NoShowLimit, s.NoShowWindowDays, s.LimitedUpcomingBookings,
                s.PointsPerSar, s.SilverGames, s.GoldGames, s.SilverMultiplier, s.GoldMultiplier,
                s.TierWindowDays, s.PointsPerRedeemStep, s.SarPerRedeemStep, s.MaxRedeemPercent);
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
            if (input.PointsPerSar is < 0 or > 100) return ApiErrors.Fail(400, "Give between 0 and 100 points per SAR.");
            if (input.SilverGames is < 1 or > 500 || input.GoldGames is < 1 or > 500)
                return ApiErrors.Fail(400, "Tiers need between 1 and 500 games.");
            if (input.GoldGames <= input.SilverGames) return ApiErrors.Fail(400, "Gold must need more games than Silver.");
            if (input.SilverMultiplier is < 1 or > 5 || input.GoldMultiplier is < 1 or > 5)
                return ApiErrors.Fail(400, "Tier multipliers must be between 1 and 5.");
            if (input.TierWindowDays is < 7 or > 365) return ApiErrors.Fail(400, "Count games over 7 to 365 days.");
            if (input.PointsPerRedeemStep is < 1 or > 100_000) return ApiErrors.Fail(400, "Points per step must be between 1 and 100,000.");
            if (input.SarPerRedeemStep is <= 0 or > 1000) return ApiErrors.Fail(400, "Each step must be worth more than SAR 0 and at most SAR 1,000.");
            if (input.MaxRedeemPercent is < 0 or > 100) return ApiErrors.Fail(400, "Points can cover between 0 and 100% of the amount.");

            var settings = await db.PlatformSettings.SingleAsync(ct);
            settings.ReminderHoursBefore = input.ReminderHoursBefore;
            settings.NoShowLimit = input.NoShowLimit;
            settings.NoShowWindowDays = input.NoShowWindowDays;
            settings.LimitedUpcomingBookings = input.LimitedUpcomingBookings;
            settings.PointsPerSar = input.PointsPerSar;
            settings.SilverGames = input.SilverGames;
            settings.GoldGames = input.GoldGames;
            settings.SilverMultiplier = input.SilverMultiplier;
            settings.GoldMultiplier = input.GoldMultiplier;
            settings.TierWindowDays = input.TierWindowDays;
            settings.PointsPerRedeemStep = input.PointsPerRedeemStep;
            settings.SarPerRedeemStep = input.SarPerRedeemStep;
            settings.MaxRedeemPercent = input.MaxRedeemPercent;
            await db.SaveChangesAsync(ct);
            return Results.Ok(SettingsDto.From(settings));
        });
    }
}
