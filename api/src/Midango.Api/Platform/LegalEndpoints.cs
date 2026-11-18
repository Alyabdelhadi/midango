using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;
using Npgsql;

namespace Midango.Api.Platform;

public static class LegalEndpoints
{
    public sealed record LegalDto(string Kind, string BodyEn, string BodyAr, DateTimeOffset UpdatedAt)
    {
        public static LegalDto From(LegalDocument d) => new(d.Kind, d.BodyEn, d.BodyAr, d.UpdatedAt);
    }

    public sealed record LegalBody(string? BodyEn, string? BodyAr);

    public static void MapLegalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/legal/{kind}", async (string kind, HttpContext http, MidangoDb db, CancellationToken ct) =>
        {
            var doc = await db.LegalDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Kind == kind, ct);
            if (doc is null) return ApiErrors.Fail(404, "Page not found.");
            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Ok(LegalDto.From(doc));
        });

        var admin = app.MapGroup("/admin/legal").RequireAuthorization(Policies.PlatformOperator);

        admin.MapGet("/{kind}", async (string kind, MidangoDb db, CancellationToken ct) =>
            await db.LegalDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Kind == kind, ct) is { } doc
                ? Results.Ok(LegalDto.From(doc))
                : ApiErrors.Fail(404, "Page not found."));

        admin.MapPut("/{kind}", async (string kind, LegalBody body, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            if (!LegalDocument.Kinds.Contains(kind)) return ApiErrors.Fail(404, "Page not found.");
            if (string.IsNullOrWhiteSpace(body.BodyEn) || string.IsNullOrWhiteSpace(body.BodyAr))
                return ApiErrors.Fail(400, "Write both the English and the Arabic text.");
            if (body.BodyEn.Length > LegalDocument.MaxBodyLength || body.BodyAr.Length > LegalDocument.MaxBodyLength)
                return ApiErrors.Fail(400, "Keep each language under 60,000 characters.");

            var doc = await db.LegalDocuments.SingleOrDefaultAsync(d => d.Kind == kind, ct);
            if (doc is null)
            {
                doc = new LegalDocument { Kind = kind, BodyEn = "", BodyAr = "" };
                db.LegalDocuments.Add(doc);
            }
            doc.BodyEn = body.BodyEn;
            doc.BodyAr = body.BodyAr;
            doc.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Results.Ok(LegalDto.From(doc));
        });
    }

    /// Every environment, after migrations: inserts a missing kind from the drafts shipped next to the
    /// app (Platform/Legal/{kind}.{en,ar}.md). Never overwrites, so the operator's edits survive.
    /// Skips quietly while the table doesn't exist yet (production migrations not applied).
    public static async Task SeedLegalAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MidangoDb>();
        var log = scope.ServiceProvider.GetRequiredService<ILogger<LegalDocument>>();
        try
        {
            var table = await db.Database
                .SqlQuery<string?>($"SELECT to_regclass('legal_documents')::text AS \"Value\"").SingleAsync();
            if (table is null)
            {
                log.LogWarning("legal_documents does not exist yet; apply migrations, then restart to seed the legal pages");
                return;
            }

            var existing = await db.LegalDocuments.Select(d => d.Kind).ToListAsync();
            var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
            var dir = Path.Combine(AppContext.BaseDirectory, "Platform", "Legal");
            foreach (var kind in LegalDocument.Kinds.Except(existing))
            {
                db.LegalDocuments.Add(new LegalDocument
                {
                    Kind = kind,
                    BodyEn = await File.ReadAllTextAsync(Path.Combine(dir, $"{kind}.en.md")),
                    BodyAr = await File.ReadAllTextAsync(Path.Combine(dir, $"{kind}.ar.md")),
                    UpdatedAt = now,
                });
            }
            await db.SaveChangesAsync();
        }
        catch (NpgsqlException ex)
        {
            // Same stance as /health: an unreachable database at startup is reported, not fatal.
            log.LogError(ex, "Could not seed the legal pages");
        }
    }
}
