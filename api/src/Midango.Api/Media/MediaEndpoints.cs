using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Catalog;
using Midango.Api.Data;
using Midango.Api.Identity;
using Midango.Api.Venues;

namespace Midango.Api.Media;

/// Uploads: a venue's logo and photos (its manager), sport and feature icons (the operator).
/// Bearer-token APIs, not cookie forms, so antiforgery is off. Replaced files are deleted after the save.
public static class MediaEndpoints
{
    public const int MaxPhotos = 10;

    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var venue = app.MapGroup("/venue").RequireAuthorization(Policies.VenueManager);

        venue.MapPut("/logo", async (IFormFile? file, ClaimsPrincipal user, MidangoDb db, MediaStore media, CancellationToken ct) =>
        {
            var v = await db.Venues.FindAsync([user.StaffVenueId()!.Value], ct);
            if (v is null) return ApiErrors.Fail(404, "Venue not found.");
            var (path, error) = await media.SaveAsync(file, MediaKind.Icon, ct);
            if (error is not null) return ApiErrors.Fail(400, error);
            var old = v.LogoPath;
            v.LogoPath = path;
            await db.SaveChangesAsync(ct);
            media.Delete(old);
            return Results.Ok(new { logoUrl = path });
        }).DisableAntiforgery();

        venue.MapDelete("/logo", async (ClaimsPrincipal user, MidangoDb db, MediaStore media, CancellationToken ct) =>
        {
            var v = await db.Venues.FindAsync([user.StaffVenueId()!.Value], ct);
            if (v is null) return ApiErrors.Fail(404, "Venue not found.");
            var old = v.LogoPath;
            v.LogoPath = null;
            await db.SaveChangesAsync(ct);
            media.Delete(old);
            return Results.NoContent();
        });

        venue.MapPost("/photos", async (IFormFile? file, ClaimsPrincipal user, MidangoDb db, MediaStore media, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var photos = await db.VenuePhotos.Where(p => p.VenueId == venueId).ToListAsync(ct);
            // ponytail: count check without a lock; two simultaneous uploads can make 11. Lock the venue row if that matters.
            if (photos.Count >= MaxPhotos) return ApiErrors.Fail(409, "A venue can have up to 10 photos.");
            var (path, error) = await media.SaveAsync(file, MediaKind.Photo, ct);
            if (error is not null) return ApiErrors.Fail(400, error);
            db.VenuePhotos.Add(new VenuePhoto
            {
                VenueId = venueId, Path = path!,
                SortOrder = photos.Count == 0 ? 0 : photos.Max(p => p.SortOrder) + 1,
            });
            await db.SaveChangesAsync(ct);
            return Results.Ok(await VenueEndpoints.PhotosAsync(db, venueId, ct));
        }).DisableAntiforgery();

        venue.MapDelete("/photos/{id:guid}", async (Guid id, ClaimsPrincipal user, MidangoDb db, MediaStore media, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var photo = await db.VenuePhotos.SingleOrDefaultAsync(p => p.Id == id && p.VenueId == venueId, ct);
            if (photo is null) return ApiErrors.Fail(404, "Photo not found.");
            db.VenuePhotos.Remove(photo);
            await db.SaveChangesAsync(ct);
            media.Delete(photo.Path);
            return Results.Ok(await VenueEndpoints.PhotosAsync(db, venueId, ct));
        });

        venue.MapPut("/photos/order", async (Guid[] order, ClaimsPrincipal user, MidangoDb db, CancellationToken ct) =>
        {
            var venueId = user.StaffVenueId()!.Value;
            var photos = await db.VenuePhotos.Where(p => p.VenueId == venueId).ToListAsync(ct);
            if (order.Length != photos.Count || !order.ToHashSet().SetEquals(photos.Select(p => p.Id)))
                return ApiErrors.Fail(400, "Send every photo once.");
            foreach (var p in photos) p.SortOrder = Array.IndexOf(order, p.Id);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await VenueEndpoints.PhotosAsync(db, venueId, ct));
        });

        var op = app.MapGroup("/admin").RequireAuthorization(Policies.PlatformOperator);

        op.MapPut("/categories/{id:guid}/icon", async (Guid id, IFormFile? file, MidangoDb db, MediaStore media, CancellationToken ct) =>
        {
            var category = await db.SportCategories.SingleOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return ApiErrors.Fail(404, "Category not found.");
            var (path, error) = await media.SaveAsync(file, MediaKind.Icon, ct);
            if (error is not null) return ApiErrors.Fail(400, error);
            var old = category.IconPath;
            category.IconPath = path;
            await db.SaveChangesAsync(ct);
            media.Delete(old);
            return Results.Ok(await CategoryAsync(db, id, ct));
        }).DisableAntiforgery();

        op.MapDelete("/categories/{id:guid}/icon", async (Guid id, MidangoDb db, MediaStore media, CancellationToken ct) =>
        {
            var category = await db.SportCategories.SingleOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return ApiErrors.Fail(404, "Category not found.");
            var old = category.IconPath;
            category.IconPath = null;
            await db.SaveChangesAsync(ct);
            media.Delete(old);
            return Results.Ok(await CategoryAsync(db, id, ct));
        });

        op.MapPut("/features/{id:guid}/icon", async (Guid id, IFormFile? file, MidangoDb db, MediaStore media, CancellationToken ct) =>
        {
            var feature = await db.CourtFeatures.SingleOrDefaultAsync(f => f.Id == id, ct);
            if (feature is null) return ApiErrors.Fail(404, "Feature not found.");
            var (path, error) = await media.SaveAsync(file, MediaKind.Icon, ct);
            if (error is not null) return ApiErrors.Fail(400, error);
            var old = feature.IconPath;
            feature.IconPath = path;
            await db.SaveChangesAsync(ct);
            media.Delete(old);
            return Results.Ok(await CategoryAsync(db, feature.CategoryId, ct));
        }).DisableAntiforgery();

        op.MapDelete("/features/{id:guid}/icon", async (Guid id, MidangoDb db, MediaStore media, CancellationToken ct) =>
        {
            var feature = await db.CourtFeatures.SingleOrDefaultAsync(f => f.Id == id, ct);
            if (feature is null) return ApiErrors.Fail(404, "Feature not found.");
            var old = feature.IconPath;
            feature.IconPath = null;
            await db.SaveChangesAsync(ct);
            media.Delete(old);
            return Results.Ok(await CategoryAsync(db, feature.CategoryId, ct));
        });
    }

    private static async Task<CategoryEndpoints.CategoryDto> CategoryAsync(MidangoDb db, Guid id, CancellationToken ct) =>
        CategoryEndpoints.CategoryDto.From(await db.SportCategories.AsNoTracking().Include(c => c.Features).SingleAsync(c => c.Id == id, ct));
}
