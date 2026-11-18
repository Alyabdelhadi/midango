using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Platform;

public static partial class LeadEndpoints
{
    /// `Website` is a honeypot: hidden on the page, so only bots fill it.
    public sealed record LeadBody(
        string? VenueName, string? ContactName, string? Phone, string? City, int Courts,
        string? Message, string? Language, string? Website);

    public sealed record LeadDto(
        Guid Id, string VenueName, string ContactName, string Phone, string City, int Courts,
        string? Message, string Language, DateTimeOffset CreatedAt, DateTimeOffset? ContactedAt);

    public sealed record ContactedBody(bool Contacted);

    [GeneratedRegex(@"^\+?[0-9 ()-]{6,24}$")]
    private static partial Regex PhoneShape();

    public static void MapLeadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/leads", async (LeadBody body, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            if (!string.IsNullOrEmpty(body.Website)) return Results.Accepted(); // bot: pretend it worked

            static string? Clean(string? s, int max) =>
                string.IsNullOrWhiteSpace(s) ? null : s.Trim() is var t && t.Length <= max ? t : null;

            var venue = Clean(body.VenueName, 120);
            var name = Clean(body.ContactName, 80);
            var phone = Clean(body.Phone, 32);
            var city = Clean(body.City, 80);
            if (venue is null) return ApiErrors.Fail(400, "Enter the venue name (up to 120 characters).");
            if (name is null) return ApiErrors.Fail(400, "Enter your name (up to 80 characters).");
            if (phone is null || !PhoneShape().IsMatch(phone)) return ApiErrors.Fail(400, "Enter a phone number we can call.");
            if (city is null) return ApiErrors.Fail(400, "Enter the city (up to 80 characters).");
            if (body.Courts is < 1 or > 100) return ApiErrors.Fail(400, "Enter between 1 and 100 courts.");
            var message = body.Message?.Trim();
            if (message is { Length: > 1000 }) return ApiErrors.Fail(400, "Keep the message under 1000 characters.");

            db.VenueLeads.Add(new VenueLead
            {
                VenueName = venue, ContactName = name, Phone = phone, City = city, Courts = body.Courts,
                Message = string.IsNullOrEmpty(message) ? null : message,
                Language = body.Language == "ar" ? "ar" : "en",
                CreatedAt = clock.GetUtcNow(),
            });
            await db.SaveChangesAsync(ct);
            return Results.Accepted();
        }).RequireRateLimiting(Policies.AuthRateLimit);

        var admin = app.MapGroup("/admin/leads").RequireAuthorization(Policies.PlatformOperator);

        admin.MapGet("", async (MidangoDb db, CancellationToken ct) =>
            Results.Ok(await db.VenueLeads.AsNoTracking().OrderByDescending(l => l.CreatedAt).Take(200)
                .Select(l => new LeadDto(l.Id, l.VenueName, l.ContactName, l.Phone, l.City, l.Courts,
                    l.Message, l.Language, l.CreatedAt, l.ContactedAt))
                .ToListAsync(ct)));

        admin.MapPut("/{id:guid}/contacted", async (Guid id, ContactedBody body, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var lead = await db.VenueLeads.SingleOrDefaultAsync(l => l.Id == id, ct);
            if (lead is null) return ApiErrors.Fail(404, "Request not found.");
            lead.ContactedAt = body.Contacted ? lead.ContactedAt ?? clock.GetUtcNow() : null;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new LeadDto(lead.Id, lead.VenueName, lead.ContactName, lead.Phone, lead.City,
                lead.Courts, lead.Message, lead.Language, lead.CreatedAt, lead.ContactedAt));
        });
    }
}
