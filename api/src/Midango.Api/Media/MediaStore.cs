namespace Midango.Api.Media;

public enum MediaKind { Photo, Icon }

/// Stores uploaded images on disk and serves them at /media. Formats are checked from the bytes, never the name.
/// ponytail: local disk; move to object storage + CDN for production. No resizing; add a resize step when photo sizes hurt the app.
public sealed class MediaStore(IConfiguration config, IWebHostEnvironment env)
{
    public const long MaxPhotoBytes = 5 * 1024 * 1024;
    public const long MaxIconBytes = 512 * 1024;
    public string Root { get; } = Directory.CreateDirectory(
        config["Media:Path"] is { Length: > 0 } p ? p : Path.Combine(env.ContentRootPath, "uploads")).FullName;

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
