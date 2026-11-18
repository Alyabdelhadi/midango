using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;

namespace Midango.Api.Notifications;

/// Tournament pushes to team captains, in each device's language. Call after the change is committed:
/// failures are logged and never reach the request. Data { tournamentId } opens the tournament page.
public sealed class TournamentNotifications(MidangoDb db, IPushSender push, ILogger<TournamentNotifications> log)
{
    public Task CancelledAsync(Guid tournamentId, IReadOnlyCollection<Guid> captains) =>
        SendAsync(tournamentId, captains, (_, ar, t) => ar
            ? ($"أُلغيت بطولة {t.NameAr}", $"{t.VenueAr} · {t.When}")
            : ($"{t.NameEn} was cancelled", $"{t.VenueEn} · {t.When}"));

    /// A waitlisted team got a place.
    public Task PromotedAsync(Guid tournamentId, IReadOnlyCollection<Guid> captains) =>
        SendAsync(tournamentId, captains, (_, ar, t) => ar
            ? ($"تم قبول فريقك في {t.NameAr}", $"{t.VenueAr} · {t.When}")
            : ($"You're in: {t.NameEn}", $"{t.VenueEn} · {t.When}"));

    /// A drawn team's first match (UTC start), or a bye when Opponent is null.
    public sealed record DrawNotice(Guid CaptainId, string? Opponent, DateTimeOffset? StartsAt, string? CourtEn, string? CourtAr);

    /// The draw is out: each captain hears their own first match.
    public Task DrawAsync(Guid tournamentId, IReadOnlyList<DrawNotice> notices)
    {
        var byCaptain = notices.ToDictionary(n => n.CaptainId);
        return SendAsync(tournamentId, byCaptain.Keys.ToList(), (captain, ar, t) =>
        {
            var n = byCaptain[captain];
            if (n.Opponent is null)
                return ar ? ("لديك تأهل مباشر في الدور الأول", $"{t.NameAr} · {t.VenueAr}")
                          : ("You have a bye in round 1", $"{t.NameEn} · {t.VenueEn}");
            var at = n.StartsAt is { } s ? t.Local(s) : "";
            return ar ? ($"القرعة جاهزة: {n.Opponent} في {at} على {n.CourtAr}", $"{t.NameAr} · {t.VenueAr}")
                      : ($"The draw is out: {n.Opponent} at {at} on {n.CourtEn}", $"{t.NameEn} · {t.VenueEn}");
        });
    }

    public Task ChampionAsync(Guid tournamentId, string teamName, IReadOnlyCollection<Guid> captains) =>
        SendAsync(tournamentId, captains, (_, ar, t) => ar
            ? ($"{teamName} فاز ببطولة {t.NameAr}", t.VenueAr)
            : ($"{teamName} won {t.NameEn}", t.VenueEn));

    private sealed record Info(string NameEn, string NameAr, string VenueEn, string VenueAr, string When, TimeZoneInfo Zone)
    {
        public string Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone).ToString("dd/MM HH:mm");
    }

    /// text(captain, arabic, info). Devices whose text comes out the same go in one send.
    private async Task SendAsync(Guid tournamentId, IReadOnlyCollection<Guid> to, Func<Guid, bool, Info, (string Title, string Body)> text)
    {
        if (to.Count == 0) return;
        try
        {
            var ct = CancellationToken.None; // the request may be gone; the push still goes out
            var row = await (from t in db.Tournaments
                             join v in db.Venues on t.VenueId equals v.Id
                             where t.Id == tournamentId
                             select new { t.NameEn, t.NameAr, t.StartsAt, VenueEn = v.NameEn, VenueAr = v.NameAr, v.TimeZoneId })
                .AsNoTracking().SingleAsync(ct);
            var zone = TimeZoneInfo.FindSystemTimeZoneById(row.TimeZoneId);
            var info = new Info(row.NameEn, row.NameAr, row.VenueEn, row.VenueAr, TimeZoneInfo.ConvertTime(row.StartsAt, zone).ToString("dd/MM HH:mm"), zone);

            var devices = await db.CustomerDevices.AsNoTracking().Where(d => to.Contains(d.CustomerId)).ToListAsync(ct);
            var data = new Dictionary<string, string> { ["tournamentId"] = tournamentId.ToString() };
            foreach (var group in devices.GroupBy(d => text(d.CustomerId, d.Language == "ar", info)))
            {
                var (title, body) = group.Key;
                var dead = await push.SendAsync(group.Select(d => d.Token).ToList(), new PushMessage(title, body, data), ct);
                if (dead.Count > 0) await db.CustomerDevices.Where(d => dead.Contains(d.Token)).ExecuteDeleteAsync(ct);
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Tournament push for {TournamentId} failed", tournamentId);
        }
    }
}
