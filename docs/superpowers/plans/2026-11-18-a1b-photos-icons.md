# A1b Photos & Icons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Venues get a logo and up to 10 ordered photos; sport categories and court features get optional uploaded icons. The admin uploads them; the app shows them.

**Design (2026-11-18):**
- **Storage:** `MediaStore` (singleton) writes files to `Media:Path` (default `{ContentRootPath}/media`, git-ignored) with random names `{guid:N}.{ext}` and deletes replaced/removed files. The API serves them at `/media/{file}` (static files, `Cache-Control: public, max-age=31536000, immutable` — names never change). *ponytail: local disk; move to object storage + CDN for production.*
- **Formats:** JPEG, PNG, WebP only, detected from the first bytes (not the file name or content type). Photos ≤ 5 MB; logos and icons ≤ 512 KB. No SVG (can carry script). No resizing (*ponytail: add a resize step when photo sizes hurt the app*).
- **URLs in JSON** are root-relative (`/media/abc.webp`) or `null`. The admin loads them through its proxy as `/api` + url; the app as `apiBase` + url.
- **Venue:** `Venue.LogoPath`; `VenuePhoto { Id, VenueId, Path, SortOrder, CreatedAt }`, cascade with the venue, max 10.
- **Category / feature:** `SportCategory.IconPath`, `CourtFeature.IconPath`, both optional; the built-in `IconKey` stays as the fallback.
- Upload endpoints take `multipart/form-data` with one field `file` and call `.DisableAntiforgery()` (they're bearer-token APIs, not cookie-authenticated forms).

**Tech Stack:** .NET 10 minimal API + EF Core/Npgsql; React 19 admin; Flutter app (plain widgets, `Image.network`).

Project rules: **no automated tests** — verify by building and running. Bilingual EN + AR with RTL.

---

### Task 1: API

**Files:** create `api/src/Midango.Api/Media/MediaStore.cs`, `Media/MediaEndpoints.cs`; modify `Venues/Venue.cs`, `Catalog/Entities.cs`, `Data/MidangoDb.cs`, `Catalog/VenueEndpoints.cs`, `Catalog/CategoryEndpoints.cs`, `Catalog/PublicVenueEndpoints.cs`, `Program.cs`, `.gitignore` (repo root: add `api/src/Midango.Api/media/`); migration `Media`.

- [ ] `MediaStore`:

```csharp
namespace Midango.Api.Media;

public enum MediaKind { Photo, Icon }

/// Stores uploaded images on disk and serves them at /media. Formats are checked from the bytes, never the name.
public sealed class MediaStore(IConfiguration config, IWebHostEnvironment env)
{
    public const long MaxPhotoBytes = 5 * 1024 * 1024;
    public const long MaxIconBytes = 512 * 1024;
    public string Root { get; } = Directory.CreateDirectory(
        config["Media:Path"] is { Length: > 0 } p ? p : Path.Combine(env.ContentRootPath, "media")).FullName;

    /// Saves the file and returns its public path ("/media/…"), or an error sentence for the person uploading.
    public async Task<(string? Path, string? Error)> SaveAsync(IFormFile? file, MediaKind kind, CancellationToken ct)
    {
        var max = kind == MediaKind.Photo ? MaxPhotoBytes : MaxIconBytes;
        if (file is null || file.Length == 0) return (null, "Choose an image to upload.");
        if (file.Length > max) return (null, kind == MediaKind.Photo ? "Photos can be up to 5 MB." : "Logos and icons can be up to 512 KB.");
        var head = new byte[12];
        await using (var s = file.OpenReadStream()) _ = await s.ReadAtLeastAsync(head, head.Length, false, ct);
        var ext = Sniff(head);
        if (ext is null) return (null, "Use a JPEG, PNG or WebP image.");
        var name = $"{Guid.NewGuid():N}.{ext}";
        await using (var target = File.Create(System.IO.Path.Combine(Root, name)))
            await file.CopyToAsync(target, ct);
        return ($"/media/{name}", null);
    }

    public void Delete(string? publicPath)
    {
        if (publicPath is null || !publicPath.StartsWith("/media/")) return;
        var full = System.IO.Path.Combine(Root, System.IO.Path.GetFileName(publicPath));
        if (File.Exists(full)) File.Delete(full);
    }

    private static string? Sniff(ReadOnlySpan<byte> b) =>
        b[..3].SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }) ? "jpg"
        : b[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) ? "png"
        : b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8) ? "webp"
        : null;
}
```

  Register `builder.Services.AddSingleton<MediaStore>();`. Serve: after `app.UseCors();` add `app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(media.Root), RequestPath = "/media", OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable" });` where `media = app.Services.GetRequiredService<MediaStore>()`.

- [ ] Entities: `Venue.LogoPath` (`string?`, max 200), `SportCategory.IconPath`, `CourtFeature.IconPath` (`string?`, max 200), new `VenuePhoto` in `Venues/` (`Path` max 200, index `(VenueId, SortOrder)`, FK cascade). `DbSet<VenuePhoto> VenuePhotos`.
- [ ] `MediaEndpoints.MapMediaEndpoints` (register in Program after `MapOperatorEndpoints`). Every upload endpoint: `.DisableAntiforgery()`; a `SaveAsync` error → 400 with that sentence; on success, delete the replaced file.
  - Venue (group `/venue`, `Policies.VenueManager`, venue = `user.StaffVenueId()`):
    - `PUT /venue/logo` (form `file`, Icon kind) → `{ logoUrl }`. `DELETE /venue/logo` → 204.
    - `POST /venue/photos` (Photo kind) → 409 "A venue can have up to 10 photos." when full; appends with `SortOrder = max + 1`; returns the full photo list `[{ id, url }]` in order. `DELETE /venue/photos/{id}` → list. `PUT /venue/photos/order` body `Guid[]` (must be exactly the venue's photo ids, else 400 "Send every photo once.") → list.
  - Operator (`Policies.PlatformOperator`): `PUT /admin/categories/{id}/icon`, `DELETE /admin/categories/{id}/icon`, `PUT /admin/features/{id}/icon`, `DELETE /admin/features/{id}/icon` → the updated `CategoryDto` (reload with features).
- [ ] DTOs (add fields at the end of each record, camelCase in JSON):
  - `CategoryEndpoints.FeatureDto` + `string? IconUrl`; `CategoryDto` + `string? IconUrl` (and the public `/categories` list gets the field too).
  - `VenueEndpoints` manager venue DTO (`GET /venue`) + `string? LogoUrl`, `IReadOnlyList<PhotoDto> Photos` where `PhotoDto(Guid Id, string Url)`.
  - `PublicVenueEndpoints`: `VenueCardDto` + `string? LogoUrl, string? PhotoUrl` (first photo); `PublicFeatureDto` + `string? IconUrl`; `PublicCategoryDto` + `string? IconUrl`; `PublicVenueDto` + `string? LogoUrl, IReadOnlyList<string> Photos`.
- [ ] Build, `dotnet ef migrations add Media`, build. Commit `git add api .gitignore` — `feat(api): venue logos and photos, sport and feature icons` + co-author line.

### Task 2: Admin

**Files:** `admin/src/lib/api.ts`, `lib/types.ts`, `pages/venue/*` (the Venue page), `pages/CategoriesPage.tsx`, `pages/CategoryDialog.tsx`, `lib/sportIcons.tsx`, locales, CSS.

- [ ] `api.ts`: `Options.method` adds `'DELETE'`; `body` may be a `FormData` — then send it as-is and **don't** set `Content-Type` (the browser adds the boundary). Add `export const mediaUrl = (url: string) => '/api' + url;`.
- [ ] Venue page: a "Logo and photos" card (manager and acting operator can edit; desk staff read-only like the rest of the page). Logo: 96px preview (or placeholder), **Upload logo** (`<input type="file" accept="image/jpeg,image/png,image/webp">` behind a button), **Remove**. Photos: grid of thumbnails (16:10, `object-fit: cover`), each with **Move earlier / Move later** (icon buttons with labels, order mirrors in RTL) and **Remove**; **Add photos** accepts multiple files and uploads them one by one; shows "{n} of 10". API errors via toast. Hint text: "JPEG, PNG or WebP. Photos up to 5 MB, logo up to 512 KB."
- [ ] Categories: `SportIcon` gains an optional `iconUrl` — when set, render `<img src={mediaUrl(iconUrl)} alt="" width/height={size}>` instead of the Lucide icon. Categories list uses it. In `CategoryDialog` (editing an existing category only): an "Uploaded icon" row with preview, **Upload icon**, **Remove** ("Overrides the icon chosen above"); for each existing feature, a small icon upload/remove control. New categories/features: hint "Save first, then add an icon."
- [ ] Types + EN/AR locale keys for every new string. `npm run build && npm run lint` → 0 errors. **Do not commit** (controller commits).

### Task 3: App (Flutter)

**Files:** `app/lib/api/models.dart`, `app/lib/play/venue_card.dart`, `app/lib/venue/venue_page.dart`, `app/lib/play/pickers.dart`, `app/lib/widgets/sport_icon.dart`, maybe a new `app/lib/widgets/media_image.dart`.

- [ ] Models: `Category.iconUrl`, `VenueCard.logoUrl` / `photoUrl`, venue detail `logoUrl` / `photos`, feature `iconUrl` (all nullable; photos default empty).
- [ ] `MediaImage` widget: `Image.network('$apiBase$url', fit: BoxFit.cover)` with a neutral placeholder while loading and on error (never a broken-image icon); decorative (no semantic label).
- [ ] Venue card: when `photoUrl` is set, a 16:9 photo at the top with rounded top corners; logo (when set) as a 40px rounded square next to the name. Without photos the card looks exactly as today.
- [ ] Venue page: when photos exist, a swipeable `PageView` gallery at the top (16:10) with page dots, works in RTL; logo next to the venue name. Features list shows the feature icon (16px) when set.
- [ ] Sport chips (`pickers.dart` / `sport_icon.dart`): when `iconUrl` is set show the image (20px) instead of the Lucide icon.
- [ ] `C:\Users\Lenovo\flutter\bin\flutter.bat analyze` from `app/` (PowerShell only — never run flutter from bash) → no issues. **Do not commit.**

### Task 4: Verify (controller)
- [ ] API script: logo upload png → url served with immutable cache header; fake `.png` that is text → 400; 6 MB photo → 400; 11th photo → 409; reorder wrong ids → 400, right ids → new order; delete removes the file from disk; replacing the logo deletes the old file; category/feature icon set + remove; public `/venues`, `/venues/{id}`, `/categories` carry the urls; desk staff 403 on uploads; operator acting can upload.
- [ ] Browser: admin Venue page upload logo + 3 photos, reorder, remove (EN + AR); category icon upload shows in the list; Flutter web app shows the card photo + logo, the gallery swipes, the sport chip shows the uploaded icon.
- [ ] Append results; roadmap row; merge.
