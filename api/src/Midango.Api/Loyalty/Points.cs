using Microsoft.EntityFrameworkCore;
using Midango.Api.Bookings;
using Midango.Api.Data;
using Midango.Api.Platform;

namespace Midango.Api.Loyalty;

public enum PointsKind
{
    Earn,
    Redeem,
    EarnReversal,
    RedeemReleased,
}

/// One line of a customer's points ledger. The balance is SUM(Points) and is never stored anywhere else.
public sealed class PointsEntry
{
    public long Id { get; init; }
    public Guid CustomerId { get; init; }

    /// + earn / − redeem / ± reversal.
    public int Points { get; init; }
    public PointsKind Kind { get; init; }
    public Guid? BookingId { get; init; }
    public DateTimeOffset At { get; init; }
    public string? Note { get; init; }
}

public sealed record NextTier(string Name, int GamesNeeded);

public sealed record Tier(string Name, decimal Multiplier, int GamesInWindow, NextTier? Next);

/// Every write to the points ledger goes through here: earn and reverse at the desk, redeem from the app,
/// release when a redemption can no longer be used. Nothing here saves; the caller's SaveChanges does.
public static class Points
{
    public const string Bronze = "Bronze";
    public const string Silver = "Silver";
    public const string Gold = "Gold";

    public static Task<int> BalanceAsync(MidangoDb db, Guid customerId, CancellationToken ct) =>
        db.PointsEntries.Where(e => e.CustomerId == customerId).SumAsync(e => e.Points, ct);

    /// From checked-in games (as organiser or match player) that started in the last TierWindowDays. Never stored.
    public static async Task<Tier> TierAsync(MidangoDb db, PlatformSettings s, Guid customerId, DateTimeOffset now, CancellationToken ct)
    {
        var since = now.AddDays(-s.TierWindowDays);
        var games = await db.Bookings.CountAsync(b => b.CustomerId == customerId && b.CheckedInAt != null && b.StartsAt >= since, ct)
                    + await db.MatchPlayers.CountAsync(p => p.CustomerId == customerId && p.CheckedInAt != null
                        && db.Bookings.Any(b => b.Id == p.BookingId && b.StartsAt >= since), ct);
        return games >= s.GoldGames ? new(Gold, s.GoldMultiplier, games, null)
            : games >= s.SilverGames ? new(Silver, s.SilverMultiplier, games, new(Gold, s.GoldGames - games))
            : new(Bronze, 1, games, new(Silver, s.SilverGames - games));
    }

    /// Check-in: floor(cash paid × PointsPerSar × tier multiplier). Walk-ins without an account earn nothing.
    public static async Task EarnAsync(MidangoDb db, Guid? customerId, Guid bookingId, decimal cash, DateTimeOffset now, CancellationToken ct)
    {
        if (customerId is not { } id) return;
        var s = await db.PlatformSettings.AsNoTracking().SingleAsync(ct);
        var tier = await TierAsync(db, s, id, now, ct);
        var points = (int)Math.Floor(cash * s.PointsPerSar * tier.Multiplier);
        if (points > 0) Add(db, id, points, PointsKind.Earn, bookingId, now);
    }

    /// Undo check-in: takes back what this customer earned on this booking, net of earlier reversals.
    public static async Task ReverseEarnAsync(MidangoDb db, Guid? customerId, Guid bookingId, DateTimeOffset now, CancellationToken ct)
    {
        if (customerId is not { } id) return;
        var earned = await db.PointsEntries
            .Where(e => e.CustomerId == id && e.BookingId == bookingId && (e.Kind == PointsKind.Earn || e.Kind == PointsKind.EarnReversal))
            .SumAsync(e => e.Points, ct);
        if (earned > 0) Add(db, id, -earned, PointsKind.EarnReversal, bookingId, now);
    }

    /// SAR off for a number of points (a whole number of steps).
    public static decimal Discount(PlatformSettings s, int points) => points / s.PointsPerRedeemStep * s.SarPerRedeemStep;

    /// What is left to pay at the desk after the points discount.
    public static decimal AmountToPay(decimal amount, decimal discount) => Math.Max(0, amount - discount);

    /// The organiser's own amount: the total, or for an open match the total minus the active joiners' shares.
    public static async Task<decimal> OrganiserAmountAsync(MidangoDb db, Booking b, CancellationToken ct) =>
        b is { IsOpenMatch: true, MaxPlayers: { } max }
            ? MatchEndpoints.OrganiserShare(b.Total, max, await db.MatchPlayers.CountAsync(p => p.BookingId == b.Id && p.LeftAt == null, ct))
            : b.Total;

    /// Replaces the redemption on the organiser's amount (player null) or a joiner's share: releases the old one
    /// and spends the new one; 0 just removes it. The caller holds the customer row lock (SELECT … FOR UPDATE),
    /// so the balance read here can't be spent twice. Returns an error or null.
    public static async Task<(int Status, string Title)?> RedeemAsync(
        MidangoDb db, Guid customerId, Booking booking, MatchPlayer? player, int points, DateTimeOffset now, CancellationToken ct)
    {
        if (booking.Status != BookingStatus.Confirmed) return (409, "Only a confirmed booking can use points.");
        if (booking.StartsAt <= now) return (409, "This booking has already started.");
        if ((player is null ? booking.CheckedInAt : player.CheckedInAt) is not null) return (409, "You've already checked in at the venue.");

        var s = await db.PlatformSettings.AsNoTracking().SingleAsync(ct);
        if (points < 0 || points % s.PointsPerRedeemStep != 0) return (400, $"Use points in steps of {s.PointsPerRedeemStep}.");

        var amount = player is null
            ? await OrganiserAmountAsync(db, booking, ct)
            : MatchEndpoints.Share(booking.Total, booking.MaxPlayers!.Value);
        var cap = amount * s.MaxRedeemPercent / 100;
        var discount = Discount(s, points);
        if (discount > cap)
        {
            var maxPoints = (int)Math.Floor(cap / s.SarPerRedeemStep) * s.PointsPerRedeemStep;
            return (400, $"You can use up to {maxPoints} points on this booking.");
        }

        var held = player?.PointsRedeemed ?? booking.PointsRedeemed;
        var balance = await BalanceAsync(db, customerId, ct) + held;
        if (points > balance) return (409, $"You have {balance} points.");

        if (player is null) Release(db, booking, now);
        else Release(db, player, now);
        if (points == 0) return null;

        Add(db, customerId, -points, PointsKind.Redeem, booking.Id, now);
        if (player is null)
        {
            booking.PointsRedeemed = points;
            booking.PointsDiscount = discount;
        }
        else
        {
            player.PointsRedeemed = points;
            player.PointsDiscount = discount;
        }
        return null;
    }

    /// The organiser's redemption goes back to their balance (booking cancelled or no-show).
    // ponytail: cancel paths don't lock the booking row, so a redeem committing in the same instant as a cancel
    // can leave points held on a cancelled booking. Lock the booking in the cancel paths if that is ever seen.
    public static void Release(MidangoDb db, Booking b, DateTimeOffset now)
    {
        if (b.PointsRedeemed > 0 && b.CustomerId is { } id) Add(db, id, b.PointsRedeemed, PointsKind.RedeemReleased, b.Id, now);
        b.PointsRedeemed = 0;
        b.PointsDiscount = 0;
    }

    /// A joiner's redemption goes back to their balance (left, removed, no-show).
    public static void Release(MidangoDb db, MatchPlayer p, DateTimeOffset now)
    {
        if (p.PointsRedeemed > 0) Add(db, p.CustomerId, p.PointsRedeemed, PointsKind.RedeemReleased, p.BookingId, now);
        p.PointsRedeemed = 0;
        p.PointsDiscount = 0;
    }

    /// The whole booking is off (cancelled, no-show): the organiser's and every not-checked-in player's redemption.
    public static async Task ReleaseAllAsync(MidangoDb db, Booking b, DateTimeOffset now, CancellationToken ct)
    {
        Release(db, b, now);
        var players = await db.MatchPlayers.Where(p => p.BookingId == b.Id && p.PointsRedeemed > 0 && p.CheckedInAt == null).ToListAsync(ct);
        foreach (var p in players) Release(db, p, now);
    }

    private static void Add(MidangoDb db, Guid customerId, int points, PointsKind kind, Guid bookingId, DateTimeOffset now) =>
        db.PointsEntries.Add(new PointsEntry { CustomerId = customerId, Points = points, Kind = kind, BookingId = bookingId, At = now });
}
