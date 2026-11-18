using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Platform;

/// What the public website shows that Midango controls: the main texts, the FAQ and the contact line.
/// Store links come from AppContent. The site reads it all from the anonymous GET /site/content.
public static partial class SiteContentEndpoints
{
    public const int MaxFaqs = 20;
    public const int MaxText = 600;

    public sealed record TextDto(string Key, string En, string Ar);
    public sealed record FaqDto(Guid Id, string QuestionEn, string QuestionAr, string AnswerEn, string AnswerAr, int SortOrder);
    public sealed record ContactDto(string? Email, string? Phone, string? WhatsApp, string? Instagram);
    public sealed record SiteContentDto(
        IReadOnlyList<TextDto> Texts, IReadOnlyList<FaqDto> Faqs, ContactDto Contact,
        string? AndroidStoreUrl, string? IosStoreUrl);

    public sealed record TextInput(string? Key, string? En, string? Ar);
    public sealed record FaqInput(string? QuestionEn, string? QuestionAr, string? AnswerEn, string? AnswerAr);
    public sealed record ContactInput(string? Email, string? Phone, string? WhatsApp, string? Instagram);

    public sealed record PublicFaq(string Question, string Answer);
    public sealed record PublicSiteContent(
        IReadOnlyDictionary<string, string> Texts, IReadOnlyList<PublicFaq> Faqs, ContactDto Contact,
        string? AndroidStoreUrl, string? IosStoreUrl);

    [GeneratedRegex(@"^\+?[0-9 ]{6,30}$")]
    private static partial Regex PhoneShape();

    [GeneratedRegex(@"^\+?[0-9]{6,30}$")]
    private static partial Regex WhatsAppShape();

    [GeneratedRegex(@"^[A-Za-z0-9._]{1,60}$")]
    private static partial Regex InstagramShape();

    public static void MapSiteContentEndpoints(this IEndpointRouteBuilder app)
    {
        var op = app.MapGroup("/admin/site-content").RequireAuthorization(Policies.PlatformOperator);

        op.MapGet("", async (MidangoDb db, CancellationToken ct) => Results.Ok(await GetAsync(db, ct)));

        op.MapPut("/texts", async (TextInput[] input, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var texts = await db.SiteTexts.ToDictionaryAsync(t => t.Key, ct);
            foreach (var i in input)
            {
                if (i.Key is null || !texts.ContainsKey(i.Key)) return ApiErrors.Fail(400, "Unknown text.");
                if (Clean(i.En) is not { Length: <= MaxText } || Clean(i.Ar) is not { Length: <= MaxText })
                    return ApiErrors.Fail(400, "Write every text in English and Arabic, up to 600 characters.");
            }
            var now = clock.GetUtcNow();
            foreach (var i in input)
            {
                var text = texts[i.Key!];
                text.En = Clean(i.En)!;
                text.Ar = Clean(i.Ar)!;
                text.UpdatedAt = now;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetAsync(db, ct));
        });

        op.MapPost("/faqs", async (FaqInput input, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            if (Validate(input) is { } error) return ApiErrors.Fail(400, error);
            var existing = await db.SiteFaqs.Select(f => f.SortOrder).ToListAsync(ct);
            // ponytail: count check without a lock; two simultaneous adds can make 21. Harmless.
            if (existing.Count >= MaxFaqs) return ApiErrors.Fail(400, "You can have up to 20 questions.");
            var faq = new SiteFaq
            {
                QuestionEn = "", QuestionAr = "", AnswerEn = "", AnswerAr = "", CreatedAt = clock.GetUtcNow(),
                SortOrder = existing.Count == 0 ? 0 : existing.Max() + 1,
            };
            Apply(faq, input);
            db.SiteFaqs.Add(faq);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetAsync(db, ct));
        });

        op.MapPut("/faqs/order", async (Guid[] order, MidangoDb db, CancellationToken ct) =>
        {
            var faqs = await db.SiteFaqs.ToListAsync(ct);
            if (order.Length != faqs.Count || !order.ToHashSet().SetEquals(faqs.Select(f => f.Id)))
                return ApiErrors.Fail(400, "Send every question once.");
            foreach (var f in faqs) f.SortOrder = Array.IndexOf(order, f.Id);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetAsync(db, ct));
        });

        op.MapPut("/faqs/{id:guid}", async (Guid id, FaqInput input, MidangoDb db, CancellationToken ct) =>
        {
            var faq = await db.SiteFaqs.SingleOrDefaultAsync(f => f.Id == id, ct);
            if (faq is null) return ApiErrors.Fail(404, "Question not found.");
            if (Validate(input) is { } error) return ApiErrors.Fail(400, error);
            Apply(faq, input);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetAsync(db, ct));
        });

        op.MapDelete("/faqs/{id:guid}", async (Guid id, MidangoDb db, CancellationToken ct) =>
        {
            var faq = await db.SiteFaqs.SingleOrDefaultAsync(f => f.Id == id, ct);
            if (faq is null) return ApiErrors.Fail(404, "Question not found.");
            db.SiteFaqs.Remove(faq);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetAsync(db, ct));
        });

        op.MapPut("/contact", async (ContactInput input, MidangoDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var email = Clean(input.Email);
            var phone = Clean(input.Phone);
            var whatsApp = Clean(input.WhatsApp);
            var instagram = Clean(input.Instagram?.Trim().TrimStart('@'));
            if (email is not null && (email.Length > 120 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email))
                return ApiErrors.Fail(400, "Enter a valid email address.");
            if (phone is not null && !PhoneShape().IsMatch(phone)) return ApiErrors.Fail(400, "Enter a valid phone number.");
            if (whatsApp is not null && !WhatsAppShape().IsMatch(whatsApp))
                return ApiErrors.Fail(400, "Enter the WhatsApp number in digits.");
            if (instagram is not null && !InstagramShape().IsMatch(instagram))
                return ApiErrors.Fail(400, "Enter the Instagram handle without spaces.");

            var settings = await db.SiteSettings.SingleAsync(ct);
            settings.ContactEmail = email;
            settings.ContactPhone = phone;
            settings.WhatsApp = whatsApp;
            settings.Instagram = instagram;
            settings.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetAsync(db, ct));
        });

        app.MapGet("/site/content", async (string? lang, MidangoDb db, CancellationToken ct) =>
        {
            var ar = lang == "ar";
            var texts = await db.SiteTexts.AsNoTracking().OrderBy(t => t.SortOrder)
                .ToDictionaryAsync(t => t.Key, t => ar ? t.Ar : t.En, ct);
            var faqs = await db.SiteFaqs.AsNoTracking().OrderBy(f => f.SortOrder)
                .Select(f => new PublicFaq(ar ? f.QuestionAr : f.QuestionEn, ar ? f.AnswerAr : f.AnswerEn))
                .ToListAsync(ct);
            var s = await db.SiteSettings.AsNoTracking().SingleAsync(ct);
            var c = await db.AppContent.AsNoTracking().SingleAsync(ct);
            return Results.Ok(new PublicSiteContent(texts, faqs, Contact(s), c.AndroidStoreUrl, c.IosStoreUrl));
        });
    }

    private static async Task<SiteContentDto> GetAsync(MidangoDb db, CancellationToken ct)
    {
        var texts = await db.SiteTexts.AsNoTracking().OrderBy(t => t.SortOrder)
            .Select(t => new TextDto(t.Key, t.En, t.Ar)).ToListAsync(ct);
        var faqs = await db.SiteFaqs.AsNoTracking().OrderBy(f => f.SortOrder)
            .Select(f => new FaqDto(f.Id, f.QuestionEn, f.QuestionAr, f.AnswerEn, f.AnswerAr, f.SortOrder))
            .ToListAsync(ct);
        var s = await db.SiteSettings.AsNoTracking().SingleAsync(ct);
        var c = await db.AppContent.AsNoTracking().SingleAsync(ct);
        return new SiteContentDto(texts, faqs, Contact(s), c.AndroidStoreUrl, c.IosStoreUrl);
    }

    private static ContactDto Contact(SiteSettings s) => new(s.ContactEmail, s.ContactPhone, s.WhatsApp, s.Instagram);

    private static string? Validate(FaqInput input)
    {
        if (Clean(input.QuestionEn) is null || Clean(input.QuestionAr) is null
            || Clean(input.AnswerEn) is null || Clean(input.AnswerAr) is null)
            return "Write the question and answer in English and Arabic.";
        if (Clean(input.QuestionEn)!.Length > 200 || Clean(input.QuestionAr)!.Length > 200)
            return "Questions can be up to 200 characters.";
        if (Clean(input.AnswerEn)!.Length > 1000 || Clean(input.AnswerAr)!.Length > 1000)
            return "Answers can be up to 1000 characters.";
        return null;
    }

    private static void Apply(SiteFaq faq, FaqInput input)
    {
        faq.QuestionEn = Clean(input.QuestionEn)!;
        faq.QuestionAr = Clean(input.QuestionAr)!;
        faq.AnswerEn = Clean(input.AnswerEn)!;
        faq.AnswerAr = Clean(input.AnswerAr)!;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
