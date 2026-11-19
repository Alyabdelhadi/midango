using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Bookings;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Platform;

/// What each venue owes Midango per month: a percent of the cash it collected, or a fixed monthly fee,
/// less the points discounts Midango owes the venue. Settled months are marked paid.
public static class AccountingEndpoints
{
    public sealed record StatementRow(
        Guid VenueId, string NameEn, string NameAr, string Currency, bool Published, bool Suspended,
        FeeMode FeeMode, decimal? Percent, decimal? FeeMonthly,
        int Bookings, decimal Cash, decimal Fee, decimal PointsOwed, decimal Net,
        DateTimeOffset? PaidAt, decimal? PaidAmount);

    public sealed record CurrencyTotal(string Currency, decimal Cash, decimal Fee, decimal PointsOwed, decimal Net, decimal Paid, decimal Outstanding);
    public sealed record StatementDto(string Month, decimal DefaultPercent, IReadOnlyList<StatementRow> Rows, IReadOnlyList<CurrencyTotal> Totals);
    public sealed record PlanBody(FeeMode FeeMode, decimal? FeePercent, decimal? FeeMonthly);
    public sealed record PaidBody(bool Paid);
    public sealed record BillingSettingsDto(decimal CommissionPercent);

    private static decimal Money(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);

    private static bool TryMonth(string? text, TimeProvider clock, out DateOnly month)
    {
        if (string.IsNullOrEmpty(text))
        {
            var now = clock.GetUtcNow().UtcDateTime;
            month = new DateOnly(now.Year, now.Month, 1);
            return true;
        }
        return DateOnly.TryParseExact($"{text}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out month);
    }

    /// ponytail: months are UTC calendar months for every venue; bucket per venue time zone if a venue asks.
    private static async Task<List<StatementRow>> RowsAsync(MidangoDb db, DateOnly month, Guid? onlyVenue, CancellationToken ct)
    {
        var from = new DateTimeOffset(month.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = from.AddMonths(1);
        var defaultPercent = (await db.PlatformSettings.AsNoTracking().SingleAsync(ct)).CommissionPercent;

        var played = await db.Bookings.AsNoTracking()
            .Where(b => b.Kind == BookingKind.Booking && b.StartsAt >= from && b.StartsAt < to
                        && b.Status != BookingStatus.Cancelled && (onlyVenue == null || b.VenueId == onlyVenue))
            .Select(b => new
            {
                b.VenueId,
                // Cash the venue took: the organiser's plus every match player's share (as on the venue report).
                Cash = (b.CashCollected ?? 0) + (db.MatchPlayers.Where(p => p.BookingId == b.Id).Sum(p => p.CashCollected) ?? 0),
                // Points discounts given at check-in: Midango owes these to the venue.
                Points = (b.CheckedInAt == null ? 0 : b.PointsDiscount)
                         + db.MatchPlayers.Where(p => p.BookingId == b.Id && p.CheckedInAt != null).Sum(p => p.PointsDiscount),
            })
            .ToListAsync(ct);
        var byVenue = played.GroupBy(p => p.VenueId).ToDictionary(g => g.Key, g => (Count: g.Count(), Cash: g.Sum(p => p.Cash), Points: g.Sum(p => p.Points)));

        var paid = await db.VenueStatements.AsNoTracking()
            .Where(s => s.Month == month && (onlyVenue == null || s.VenueId == onlyVenue))
            .ToDictionaryAsync(s => s.VenueId, ct);
        var venues = await db.Venues.AsNoTracking().Where(v => onlyVenue == null || v.Id == onlyVenue).OrderBy(v => v.NameEn).ToListAsync(ct);

        var rows = new List<StatementRow>();
        foreach (var v in venues)
        {
            var (count, cash, points) = byVenue.GetValueOrDefault(v.Id);
            var percent = v.FeeMode switch { FeeMode.Default => defaultPercent, FeeMode.Percent => v.FeePercent ?? 0, _ => (decimal?)null };
            var fee = Money(v.FeeMode == FeeMode.Fixed ? v.FeeMonthly ?? 0 : cash * (percent ?? 0) / 100);
            var statement = paid.GetValueOrDefault(v.Id);
            // A draft venue with no activity, no fixed fee and nothing paid has nothing to bill.
            if (onlyVenue is null && !v.Published && !v.Suspended && count == 0 && fee == 0 && statement is null) continue;
            rows.Add(new StatementRow(
                v.Id, v.NameEn, v.NameAr, v.CurrencyCode, v.Published, v.Suspended,
                v.FeeMode, percent, v.FeeMode == FeeMode.Fixed ? v.FeeMonthly : null,
                count, Money(cash), fee, Money(points), Money(fee - points),
                statement?.PaidAt, statement?.Amount));
        }
        return rows;
    }

    public static void MapAccountingEndpoints(this IEndpointRouteBuilder app)
    {
        var accounting = app.MapGroup("/admin/accounting").RequireAuthorization(Policies.PlatformOperator);

        accounting.MapGet("", async (string? month, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            if (!TryMonth(month, clock, out var m)) return ApiErrors.Fail(400, "Choose a month like 2026-11.");
            var rows = await RowsAsync(db, m, null, ct);
            var totals = rows.GroupBy(r => r.Currency).Select(g => new CurrencyTotal(
                    g.Key, g.Sum(r => r.Cash), g.Sum(r => r.Fee), g.Sum(r => r.PointsOwed), g.Sum(r => r.Net),
                    g.Where(r => r.PaidAt != null).Sum(r => r.PaidAmount ?? 0),
                    g.Where(r => r.PaidAt == null).Sum(r => r.Net)))
                .ToList();
            var defaultPercent = (await db.PlatformSettings.AsNoTracking().SingleAsync(ct)).CommissionPercent;
            return Results.Ok(new StatementDto(m.ToString("yyyy-MM", CultureInfo.InvariantCulture), defaultPercent, rows, totals));
        });

        accounting.MapPut("/venues/{id:guid}/plan", async (Guid id, PlanBody body, MidangoDb db, CancellationToken ct) =>
        {
            var venue = await db.Venues.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (venue is null) return ApiErrors.Fail(404, "Venue not found.");
            switch (body.FeeMode)
            {
                case FeeMode.Percent when body.FeePercent is not ({ } p and >= 0 and <= 100):
                    return ApiErrors.Fail(400, "Enter a percentage between 0 and 100.");
                case FeeMode.Fixed when body.FeeMonthly is not ({ } f and >= 0 and <= 1_000_000):
                    return ApiErrors.Fail(400, "Enter a monthly fee between 0 and 1,000,000.");
            }
            venue.FeeMode = body.FeeMode;
            venue.FeePercent = body.FeeMode == FeeMode.Percent ? Money(body.FeePercent!.Value) : null;
            venue.FeeMonthly = body.FeeMode == FeeMode.Fixed ? Money(body.FeeMonthly!.Value) : null;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        accounting.MapPut("/statements/{venueId:guid}/{month}", async (
            Guid venueId, string month, PaidBody body, ClaimsPrincipal user, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            if (!TryMonth(month, clock, out var m)) return ApiErrors.Fail(400, "Choose a month like 2026-11.");
            var existing = await db.VenueStatements.SingleOrDefaultAsync(s => s.VenueId == venueId && s.Month == m, ct);
            if (!body.Paid)
            {
                if (existing is not null) db.VenueStatements.Remove(existing);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
            if (existing is not null) return Results.NoContent();
            // The server works out the amount: what was due for that month at the moment it was marked paid.
            if ((await RowsAsync(db, m, venueId, ct)).SingleOrDefault() is not { } row) return ApiErrors.Fail(404, "Venue not found.");
            db.VenueStatements.Add(new VenueStatement
            {
                VenueId = venueId, Month = m, Amount = row.Net, Currency = row.Currency,
                PaidAt = clock.GetUtcNow(), PaidByStaffId = user.SubjectId(),
            });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        var billing = app.MapGroup("/admin/settings/billing").RequireAuthorization(Policies.PlatformOperator);
        billing.MapGet("", async (MidangoDb db, CancellationToken ct) =>
            Results.Ok(new BillingSettingsDto((await db.PlatformSettings.AsNoTracking().SingleAsync(ct)).CommissionPercent)));
        billing.MapPut("", async (BillingSettingsDto body, MidangoDb db, CancellationToken ct) =>
        {
            if (body.CommissionPercent is < 0 or > 100) return ApiErrors.Fail(400, "Enter a commission between 0 and 100%.");
            var settings = await db.PlatformSettings.SingleAsync(ct);
            settings.CommissionPercent = Money(body.CommissionPercent);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new BillingSettingsDto(settings.CommissionPercent));
        });
    }
}
