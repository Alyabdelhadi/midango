using Microsoft.EntityFrameworkCore;
using Midango.Api.Bookings;
using Midango.Api.Data;

namespace Midango.Api.Notifications;

/// Every minute: sends "Still coming?" for bookings starting within the reminder lead time.
public sealed class ReminderService(IServiceScopeFactory scopes, IPushSender push, TimeProvider clock,
    IConfiguration config, ILogger<ReminderService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(config.GetValue("Reminders:IntervalSeconds", 60));
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Reminder run failed; trying again next interval");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MidangoDb>();
        var rules = await db.PlatformSettings.AsNoTracking().SingleAsync(ct);
        var now = clock.GetUtcNow();
        var lead = TimeSpan.FromHours(rules.ReminderHoursBefore);
        var horizon = now + lead;

        var due = await db.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed && b.Kind == BookingKind.Booking && b.CustomerId != null
                        && b.CheckedInAt == null && b.ReminderSentAt == null && b.StartsAt > now && b.StartsAt <= horizon)
            .ToListAsync(ct);
        if (due.Count == 0) return;

        var courtIds = due.Select(b => b.CourtId).Distinct().ToList();
        var places = await (from c in db.Courts
                            join v in db.Venues on c.VenueId equals v.Id
                            where courtIds.Contains(c.Id)
                            select new { CourtId = c.Id, c.NameEn, c.NameAr, VenueEn = v.NameEn, VenueAr = v.NameAr, v.TimeZoneId })
            .AsNoTracking().ToDictionaryAsync(p => p.CourtId, ct);

        foreach (var booking in due)
        {
            booking.ReminderSentAt = now;
            if (booking.CreatedAt > booking.StartsAt - lead) continue; // booked inside the window: no reminder

            var devices = await db.CustomerDevices.Where(d => d.CustomerId == booking.CustomerId).ToListAsync(ct);
            var place = places[booking.CourtId];
            var time = TimeZoneInfo.ConvertTime(booking.StartsAt, TimeZoneInfo.FindSystemTimeZoneById(place.TimeZoneId)).ToString("HH:mm");
            var data = new Dictionary<string, string> { ["bookingId"] = booking.Id.ToString() };
            foreach (var group in devices.GroupBy(d => d.Language))
            {
                var message = group.Key == "ar"
                    ? new PushMessage("هل ستحضر؟",
                        $"{place.NameAr} في {place.VenueAr}، الساعة {time}. لن تتمكن من الحضور؟ ألغِ من التطبيق ليلعب غيرك.", data)
                    : new PushMessage("Still coming?",
                        $"{place.NameEn} at {place.VenueEn}, {time}. Can't make it? Cancel in the app so someone else can play.", data);
                var dead = await push.SendAsync(group.Select(d => d.Token).ToList(), message, ct);
                if (dead.Count > 0) db.CustomerDevices.RemoveRange(group.Where(d => dead.Contains(d.Token)));
            }
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("Reminders handled for {Count} bookings", due.Count);
    }
}
