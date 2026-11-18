using Microsoft.EntityFrameworkCore;
using Midango.Api.Bookings;
using Midango.Api.Data;

namespace Midango.Api.Notifications;

/// Open-match pushes, in each device's language. Call after the change is saved:
/// failures are logged and never reach the request.
public sealed class MatchNotifications(MidangoDb db, IPushSender push, ILogger<MatchNotifications> log)
{
    private sealed record Place(Guid? OrganiserId, string CourtEn, string CourtAr, string VenueEn, string VenueAr, string Time);

    /// "{name} joined your match" → organiser; and "Your match is full" → everyone when it just filled.
    public async Task JoinedAsync(Guid bookingId, string joinerFirstName, bool full)
    {
        await SendAsync(bookingId, (p, _) => p.OrganiserId is { } id ? [id] : [], (ar, p) => ar
            ? new($"انضم {joinerFirstName} إلى مباراتك", $"{p.CourtAr} · {p.Time}")
            : new($"{joinerFirstName} joined your match", $"{p.CourtEn} · {p.Time}"));
        if (full)
            await SendAsync(bookingId, (p, joiners) => p.OrganiserId is { } id ? [id, .. joiners] : joiners, (ar, p) => ar
                ? new("اكتملت مباراتك", $"{p.CourtAr} · {p.Time}")
                : new("Your match is full", $"{p.CourtEn} · {p.Time}"));
    }

    public Task RemovedAsync(Guid bookingId, Guid customerId) =>
        SendAsync(bookingId, (_, _) => [customerId], (ar, p) => ar
            ? new("تمت إزالتك من مباراة", $"{p.VenueAr} · {p.Time}")
            : new("You were removed from a match", $"{p.VenueEn} · {p.Time}"));

    /// The organiser cancelled: tells the players who were still in.
    public Task CancelledAsync(Guid bookingId) =>
        SendAsync(bookingId, (_, joiners) => joiners, (ar, p) => ar
            ? new("أُلغيت مباراة انضممت إليها", $"{p.VenueAr} · {p.Time}")
            : new("A match you joined was cancelled", $"{p.VenueEn} · {p.Time}"));

    /// `recipients` picks customers from the place (organiser) and the active joiners.
    private async Task SendAsync(
        Guid bookingId, Func<Place, Guid[], Guid[]> recipients, Func<bool, Place, (string Title, string Body)> text)
    {
        try
        {
            var ct = CancellationToken.None; // the request may be gone; the push still goes out
            var place = await (from b in db.Bookings
                               join c in db.Courts on b.CourtId equals c.Id
                               join v in db.Venues on b.VenueId equals v.Id
                               where b.Id == bookingId
                               select new { b.CustomerId, b.StartsAt, c.NameEn, c.NameAr, VenueEn = v.NameEn, VenueAr = v.NameAr, v.TimeZoneId })
                .AsNoTracking().SingleAsync(ct);
            var time = TimeZoneInfo.ConvertTime(place.StartsAt, TimeZoneInfo.FindSystemTimeZoneById(place.TimeZoneId)).ToString("HH:mm");
            var p = new Place(place.CustomerId, place.NameEn, place.NameAr, place.VenueEn, place.VenueAr, time);

            var joiners = await db.MatchPlayers.Where(m => m.BookingId == bookingId && m.LeftAt == null)
                .Select(m => m.CustomerId).ToArrayAsync(ct);
            var to = recipients(p, joiners);
            if (to.Length == 0) return;

            var devices = await db.CustomerDevices.AsNoTracking().Where(d => to.Contains(d.CustomerId)).ToListAsync(ct);
            var data = new Dictionary<string, string> { ["bookingId"] = bookingId.ToString() };
            foreach (var group in devices.GroupBy(d => d.Language))
            {
                var (title, body) = text(group.Key == "ar", p);
                var dead = await push.SendAsync(group.Select(d => d.Token).ToList(), new PushMessage(title, body, data), ct);
                if (dead.Count > 0) await db.CustomerDevices.Where(d => dead.Contains(d.Token)).ExecuteDeleteAsync(ct);
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Match push for booking {BookingId} failed", bookingId);
        }
    }
}
