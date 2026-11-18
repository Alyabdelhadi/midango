using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;

namespace Midango.Api.Notifications;

/// Tournament pushes to team captains, in each device's language. Call after the change is committed:
/// failures are logged and never reach the request. Data { tournamentId } opens the tournament page.
public sealed class TournamentNotifications(MidangoDb db, IPushSender push, ILogger<TournamentNotifications> log)
{
    public Task CancelledAsync(Guid tournamentId, IReadOnlyCollection<Guid> captains) =>
        SendAsync(tournamentId, captains, (ar, t) => ar
            ? ($"أُلغيت بطولة {t.NameAr}", $"{t.VenueAr} · {t.When}")
            : ($"{t.NameEn} was cancelled", $"{t.VenueEn} · {t.When}"));

    /// A waitlisted team got a place.
    public Task PromotedAsync(Guid tournamentId, IReadOnlyCollection<Guid> captains) =>
        SendAsync(tournamentId, captains, (ar, t) => ar
            ? ($"تم قبول فريقك في {t.NameAr}", $"{t.VenueAr} · {t.When}")
            : ($"You're in: {t.NameEn}", $"{t.VenueEn} · {t.When}"));

    private sealed record Info(string NameEn, string NameAr, string VenueEn, string VenueAr, string When);

    private async Task SendAsync(Guid tournamentId, IReadOnlyCollection<Guid> to, Func<bool, Info, (string Title, string Body)> text)
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
            var when = TimeZoneInfo.ConvertTime(row.StartsAt, TimeZoneInfo.FindSystemTimeZoneById(row.TimeZoneId)).ToString("dd/MM HH:mm");
            var info = new Info(row.NameEn, row.NameAr, row.VenueEn, row.VenueAr, when);

            var devices = await db.CustomerDevices.AsNoTracking().Where(d => to.Contains(d.CustomerId)).ToListAsync(ct);
            var data = new Dictionary<string, string> { ["tournamentId"] = tournamentId.ToString() };
            foreach (var group in devices.GroupBy(d => d.Language))
            {
                var (title, body) = text(group.Key == "ar", info);
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
