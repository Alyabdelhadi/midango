using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;
using Midango.Api.Media;
using Midango.Api.Notifications;
using Midango.Api.Tournaments;

namespace Midango.Api.Platform;

/// What the app shows that Midango controls: the banner slider, featured venues, "message all users",
/// force update and maintenance. The app reads it all from the anonymous GET /app/config.
public static class AppContentEndpoints
{
    public const int MaxBanners = 10;
    public const int MaxFeatured = 10;
    public const int MaxBroadcastHistory = 50;
    /// FCM multicast takes at most 500 tokens per call.
    public const int PushChunk = 500;

    public sealed record BannerDto(
        Guid Id, string TitleEn, string TitleAr, string? BodyEn, string? BodyAr, string? ImageUrl,
        string LinkType, string? LinkTarget, bool Active, int SortOrder);
    public sealed record FeaturedDto(Guid Id, string NameEn, string NameAr, string? LogoUrl, bool Published);
    public sealed record VenueOption(Guid Id, string NameEn, string NameAr);
    public sealed record TournamentOption(Guid Id, string NameEn, string NameAr, DateTimeOffset StartsAt);
    public sealed record AppContentDto(
        int MinAndroidBuild, int MinIosBuild, string? AndroidStoreUrl, string? IosStoreUrl,
        bool Maintenance, string? MaintenanceMessageEn, string? MaintenanceMessageAr, DateTimeOffset UpdatedAt,
        IReadOnlyList<BannerDto> Banners, IReadOnlyList<FeaturedDto> Featured,
        IReadOnlyList<VenueOption> Venues, IReadOnlyList<TournamentOption> Tournaments);

    public sealed record SettingsInput(
        int MinAndroidBuild, int MinIosBuild, string? AndroidStoreUrl, string? IosStoreUrl,
        bool Maintenance, string? MaintenanceMessageEn, string? MaintenanceMessageAr);
    public sealed record BannerInput(
        string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string? LinkType, string? LinkTarget, bool Active);
    public sealed record BroadcastInput(string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr);
    public sealed record BroadcastDto(
        Guid Id, string TitleEn, string TitleAr, string BodyEn, string BodyAr, DateTimeOffset SentAt, string? SentBy,
        int Devices, int Delivered);

    public sealed record ConfigBanner(
        Guid Id, string TitleEn, string TitleAr, string? BodyEn, string? BodyAr, string? ImageUrl, string LinkType, string? LinkTarget);
    public sealed record ConfigVenue(
        Guid Id, string NameEn, string NameAr, string? LogoUrl, string? PhotoUrl, string? AddressEn, string? AddressAr,
        IReadOnlyList<Guid> CategoryIds);
    public sealed record AppConfig(
        int MinAndroidBuild, int MinIosBuild, string? AndroidStoreUrl, string? IosStoreUrl,
        bool Maintenance, string? MaintenanceMessageEn, string? MaintenanceMessageAr,
        IReadOnlyList<ConfigBanner> Banners, IReadOnlyList<ConfigVenue> Featured);

    public static void MapAppContentEndpoints(this IEndpointRouteBuilder app)
    {
        var op = app.MapGroup("/admin").RequireAuthorization(Policies.PlatformOperator);

        op.MapGet("/app-content", async (MidangoDb db, TimeProvider clock, CancellationToken ct) =>
            Results.Ok(await GetAsync(db, clock.GetUtcNow(), ct)));

        op.MapPut("/app-content", async (SettingsInput input, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            if (input.MinAndroidBuild is < 1 or > 100_000 || input.MinIosBuild is < 1 or > 100_000)
                return ApiErrors.Fail(400, "Minimum builds must be between 1 and 100,000.");
            var android = Clean(input.AndroidStoreUrl);
            var ios = Clean(input.IosStoreUrl);
            if ((android is not null && !IsHttps(android)) || (ios is not null && !IsHttps(ios)))
                return ApiErrors.Fail(400, "Store links must be full https:// addresses.");
            var messageEn = Clean(input.MaintenanceMessageEn);
            var messageAr = Clean(input.MaintenanceMessageAr);
            if (messageEn is { Length: > 300 } || messageAr is { Length: > 300 })
                return ApiErrors.Fail(400, "Maintenance messages can be up to 300 characters.");
            if (input.Maintenance && (messageEn is null || messageAr is null))
                return ApiErrors.Fail(400, "Write the maintenance message in English and Arabic.");

            var content = await db.AppContent.SingleAsync(ct);
            content.MinAndroidBuild = input.MinAndroidBuild;
            content.MinIosBuild = input.MinIosBuild;
            content.AndroidStoreUrl = android;
            content.IosStoreUrl = ios;
            content.Maintenance = input.Maintenance;
            content.MaintenanceMessageEn = messageEn;
            content.MaintenanceMessageAr = messageAr;
            content.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetAsync(db, clock.GetUtcNow(), ct));
        });

        op.MapPost("/app-content/banners", async (BannerInput input, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow();
            if (await ValidateAsync(input, db, now, ct) is { } error) return ApiErrors.Fail(400, error);
            var existing = await db.AppBanners.Select(b => b.SortOrder).ToListAsync(ct);
            // ponytail: count check without a lock; two simultaneous adds can make 11. Harmless.
            if (existing.Count >= MaxBanners) return ApiErrors.Fail(400, "You can have up to 10 banners.");
            var banner = new AppBanner
            {
                TitleEn = "", TitleAr = "", CreatedAt = now,
                SortOrder = existing.Count == 0 ? 0 : existing.Max() + 1,
            };
            Apply(banner, input);
            db.AppBanners.Add(banner);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetAsync(db, now, ct));
        });

        op.MapPut("/app-content/banners/order", async (Guid[] order, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var banners = await db.AppBanners.ToListAsync(ct);
            if (order.Length != banners.Count || !order.ToHashSet().SetEquals(banners.Select(b => b.Id)))
                return ApiErrors.Fail(400, "Send every banner once.");
            foreach (var b in banners) b.SortOrder = Array.IndexOf(order, b.Id);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetAsync(db, clock.GetUtcNow(), ct));
        });

        op.MapPut("/app-content/banners/{id:guid}", async (Guid id, BannerInput input, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow();
            var banner = await db.AppBanners.SingleOrDefaultAsync(b => b.Id == id, ct);
            if (banner is null) return ApiErrors.Fail(404, "Banner not found.");
            if (await ValidateAsync(input, db, now, ct) is { } error) return ApiErrors.Fail(400, error);
            Apply(banner, input);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetAsync(db, now, ct));
        });

        op.MapDelete("/app-content/banners/{id:guid}", async (Guid id, MidangoDb db, MediaStore media, TimeProvider clock, CancellationToken ct) =>
        {
            var banner = await db.AppBanners.SingleOrDefaultAsync(b => b.Id == id, ct);
            if (banner is null) return ApiErrors.Fail(404, "Banner not found.");
            db.AppBanners.Remove(banner);
            await db.SaveChangesAsync(ct);
            media.Delete(banner.ImagePath);
            return Results.Ok(await GetAsync(db, clock.GetUtcNow(), ct));
        });

        op.MapPut("/app-content/banners/{id:guid}/image", async (
            Guid id, IFormFile? file, MidangoDb db, MediaStore media, TimeProvider clock, CancellationToken ct) =>
        {
            var banner = await db.AppBanners.SingleOrDefaultAsync(b => b.Id == id, ct);
            if (banner is null) return ApiErrors.Fail(404, "Banner not found.");
            var (path, error) = await media.SaveAsync(file, MediaKind.Photo, ct);
            if (error is not null) return ApiErrors.Fail(400, error);
            var old = banner.ImagePath;
            banner.ImagePath = path;
            await db.SaveChangesAsync(ct);
            media.Delete(old);
            return Results.Ok(await GetAsync(db, clock.GetUtcNow(), ct));
        }).DisableAntiforgery();

        op.MapDelete("/app-content/banners/{id:guid}/image", async (Guid id, MidangoDb db, MediaStore media, TimeProvider clock, CancellationToken ct) =>
        {
            var banner = await db.AppBanners.SingleOrDefaultAsync(b => b.Id == id, ct);
            if (banner is null) return ApiErrors.Fail(404, "Banner not found.");
            var old = banner.ImagePath;
            banner.ImagePath = null;
            await db.SaveChangesAsync(ct);
            media.Delete(old);
            return Results.Ok(await GetAsync(db, clock.GetUtcNow(), ct));
        });

        op.MapPut("/app-content/featured", async (Guid[] ids, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            if (ids.Length > MaxFeatured) return ApiErrors.Fail(400, "You can feature up to 10 venues.");
            if (ids.Distinct().Count() != ids.Length) return ApiErrors.Fail(400, "Each venue can be featured once.");
            var venues = await db.Venues.Where(v => v.FeaturedRank != null || ids.Contains(v.Id)).ToListAsync(ct);
            if (ids.Any(id => venues.SingleOrDefault(v => v.Id == id) is not { Published: true, Suspended: false }))
                return ApiErrors.Fail(400, "Only published venues can be featured.");
            foreach (var v in venues)
                v.FeaturedRank = Array.IndexOf(ids, v.Id) is var i and >= 0 ? i + 1 : null;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetAsync(db, clock.GetUtcNow(), ct));
        });

        op.MapGet("/broadcasts", async (MidangoDb db, CancellationToken ct) =>
            Results.Ok(await (from b in db.Broadcasts.AsNoTracking()
                              join s in db.Staff on b.SentByStaffId equals s.Id into staff
                              from s in staff.DefaultIfEmpty()
                              orderby b.SentAt descending
                              select new BroadcastDto(b.Id, b.TitleEn, b.TitleAr, b.BodyEn, b.BodyAr, b.SentAt,
                                  s == null ? null : s.DisplayName, b.Devices, b.Delivered))
                .Take(MaxBroadcastHistory).ToListAsync(ct)));

        // ponytail: sent inside the request; move to a background job if device counts make it slow.
        op.MapPost("/broadcasts", async (
            BroadcastInput input, ClaimsPrincipal user, MidangoDb db, IPushSender push, TimeProvider clock,
            ILogger<Broadcast> log, CancellationToken ct) =>
        {
            var titleEn = Clean(input.TitleEn);
            var titleAr = Clean(input.TitleAr);
            var bodyEn = Clean(input.BodyEn);
            var bodyAr = Clean(input.BodyAr);
            if (titleEn is not { Length: <= 80 } || titleAr is not { Length: <= 80 })
                return ApiErrors.Fail(400, "Write a title of up to 80 characters in English and Arabic.");
            if (bodyEn is not { Length: <= 300 } || bodyAr is not { Length: <= 300 })
                return ApiErrors.Fail(400, "Write a message of up to 300 characters in English and Arabic.");

            var devices = await db.CustomerDevices.ToListAsync(ct);
            var data = new Dictionary<string, string> { ["kind"] = "broadcast" };
            var tried = 0;
            var dead = new HashSet<string>();
            var failed = false;
            try
            {
                foreach (var group in devices.GroupBy(d => d.Language == "ar"))
                {
                    var message = group.Key ? new PushMessage(titleAr, bodyAr, data) : new PushMessage(titleEn, bodyEn, data);
                    foreach (var chunk in group.Select(d => d.Token).Chunk(PushChunk))
                    {
                        dead.UnionWith(await push.SendAsync(chunk, message, ct));
                        tried += chunk.Length;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Broadcast stopped after {Tried} of {Total} devices", tried, devices.Count);
                failed = true;
            }

            // Whatever went out is recorded, even when sending stopped part-way.
            db.CustomerDevices.RemoveRange(devices.Where(d => dead.Contains(d.Token)));
            var row = new Broadcast
            {
                TitleEn = titleEn, TitleAr = titleAr, BodyEn = bodyEn, BodyAr = bodyAr, SentAt = clock.GetUtcNow(),
                SentByStaffId = user.SubjectId(), Devices = tried, Delivered = tried - dead.Count,
            };
            if (!failed || tried > 0) db.Broadcasts.Add(row);
            await db.SaveChangesAsync(ct);
            if (failed) return ApiErrors.Fail(502, "The message could not be sent. Try again.");

            var sentBy = await db.Staff.Where(s => s.Id == row.SentByStaffId).Select(s => s.DisplayName).SingleOrDefaultAsync(ct);
            return Results.Ok(new BroadcastDto(row.Id, row.TitleEn, row.TitleAr, row.BodyEn, row.BodyAr, row.SentAt,
                sentBy, row.Devices, row.Delivered));
        });

        app.MapGet("/app/config", async (MidangoDb db, CancellationToken ct) =>
        {
            var c = await db.AppContent.AsNoTracking().SingleAsync(ct);

            var banners = await db.AppBanners.AsNoTracking().Where(b => b.Active).OrderBy(b => b.SortOrder).ToListAsync(ct);
            var linked = banners.Where(b => b.LinkType is "Venue" or "Tournament")
                .Select(b => Guid.TryParse(b.LinkTarget, out var id) ? id : Guid.Empty).ToList();
            var liveVenues = await db.Venues.Where(v => linked.Contains(v.Id) && v.Published && !v.Suspended)
                .Select(v => v.Id).ToListAsync(ct);
            var liveTournaments = await db.Tournaments
                .Where(t => linked.Contains(t.Id) && (t.Status == TournamentStatus.Published || t.Status == TournamentStatus.InProgress))
                .Select(t => t.Id).ToListAsync(ct);
            bool Live(AppBanner b) => b.LinkType switch
            {
                "Venue" => Guid.TryParse(b.LinkTarget, out var id) && liveVenues.Contains(id),
                "Tournament" => Guid.TryParse(b.LinkTarget, out var id) && liveTournaments.Contains(id),
                _ => true,
            };

            var featured = await db.Venues.AsNoTracking()
                .Where(v => v.FeaturedRank != null && v.Published && !v.Suspended)
                .OrderBy(v => v.FeaturedRank).ToListAsync(ct);
            var ids = featured.Select(v => v.Id).ToList();
            var photos = await db.VenuePhotos.AsNoTracking()
                .Where(p => ids.Contains(p.VenueId))
                .GroupBy(p => p.VenueId)
                .Select(g => new { VenueId = g.Key, Path = g.OrderBy(p => p.SortOrder).Select(p => p.Path).First() })
                .ToDictionaryAsync(g => g.VenueId, g => g.Path, ct);
            var categories = (await (from court in db.Courts
                                     join cat in db.SportCategories on court.CategoryId equals cat.Id
                                     where ids.Contains(court.VenueId) && court.Active && cat.Active
                                     select new { court.VenueId, court.CategoryId })
                    .Distinct().ToListAsync(ct))
                .ToLookup(x => x.VenueId, x => x.CategoryId);

            return Results.Ok(new AppConfig(
                c.MinAndroidBuild, c.MinIosBuild, c.AndroidStoreUrl, c.IosStoreUrl,
                c.Maintenance, c.MaintenanceMessageEn, c.MaintenanceMessageAr,
                banners.Where(Live).Select(b => new ConfigBanner(
                    b.Id, b.TitleEn, b.TitleAr, b.BodyEn, b.BodyAr, b.ImagePath, b.LinkType, b.LinkTarget)).ToList(),
                featured.Select(v => new ConfigVenue(
                    v.Id, v.NameEn, v.NameAr, v.LogoPath, photos.GetValueOrDefault(v.Id), v.AddressEn, v.AddressAr,
                    categories[v.Id].ToList())).ToList()));
        });
    }

    private static async Task<AppContentDto> GetAsync(MidangoDb db, DateTimeOffset now, CancellationToken ct)
    {
        var c = await db.AppContent.AsNoTracking().SingleAsync(ct);
        var banners = await db.AppBanners.AsNoTracking().OrderBy(b => b.SortOrder)
            .Select(b => new BannerDto(b.Id, b.TitleEn, b.TitleAr, b.BodyEn, b.BodyAr, b.ImagePath,
                b.LinkType, b.LinkTarget, b.Active, b.SortOrder))
            .ToListAsync(ct);
        var featured = await db.Venues.AsNoTracking().Where(v => v.FeaturedRank != null).OrderBy(v => v.FeaturedRank)
            .Select(v => new FeaturedDto(v.Id, v.NameEn, v.NameAr, v.LogoPath, v.Published && !v.Suspended))
            .ToListAsync(ct);
        var venues = await db.Venues.AsNoTracking().Where(v => v.Published && !v.Suspended).OrderBy(v => v.NameEn)
            .Select(v => new VenueOption(v.Id, v.NameEn, v.NameAr)).ToListAsync(ct);
        var tournaments = await LinkableTournaments(db, now).OrderBy(t => t.StartsAt)
            .Select(t => new TournamentOption(t.Id, t.NameEn, t.NameAr, t.StartsAt)).ToListAsync(ct);
        return new AppContentDto(c.MinAndroidBuild, c.MinIosBuild, c.AndroidStoreUrl, c.IosStoreUrl,
            c.Maintenance, c.MaintenanceMessageEn, c.MaintenanceMessageAr, c.UpdatedAt,
            banners, featured, venues, tournaments);
    }

    /// Published or under way, not yet over.
    private static IQueryable<Tournament> LinkableTournaments(MidangoDb db, DateTimeOffset now) =>
        db.Tournaments.AsNoTracking().Where(t =>
            (t.Status == TournamentStatus.Published || t.Status == TournamentStatus.InProgress) && t.EndsAt > now);

    private static async Task<string?> ValidateAsync(BannerInput input, MidangoDb db, DateTimeOffset now, CancellationToken ct)
    {
        if (Clean(input.TitleEn) is not { Length: <= 80 } || Clean(input.TitleAr) is not { Length: <= 80 })
            return "Write a title of up to 80 characters in English and Arabic.";
        if (Clean(input.BodyEn) is { Length: > 200 } || Clean(input.BodyAr) is { Length: > 200 })
            return "Banner text can be up to 200 characters.";
        var target = Clean(input.LinkTarget);
        switch (input.LinkType)
        {
            case "None":
                return null;
            case "Venue":
                return Guid.TryParse(target, out var venueId)
                       && await db.Venues.AnyAsync(v => v.Id == venueId && v.Published && !v.Suspended, ct)
                    ? null : "Choose a published venue.";
            case "Tournament":
                return Guid.TryParse(target, out var tournamentId)
                       && await LinkableTournaments(db, now).AnyAsync(t => t.Id == tournamentId, ct)
                    ? null : "Choose a published tournament.";
            case "Url":
                return target is { Length: <= 500 } && IsHttps(target) ? null : "Links must be full https:// addresses.";
            default:
                return "Choose what the banner opens.";
        }
    }

    private static void Apply(AppBanner banner, BannerInput input)
    {
        banner.TitleEn = Clean(input.TitleEn)!;
        banner.TitleAr = Clean(input.TitleAr)!;
        banner.BodyEn = Clean(input.BodyEn);
        banner.BodyAr = Clean(input.BodyAr);
        banner.LinkType = input.LinkType!;
        banner.LinkTarget = input.LinkType == "None" ? null : Clean(input.LinkTarget);
        banner.Active = input.Active;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool IsHttps(string s) =>
        s.Length <= 500 && Uri.TryCreate(s, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
