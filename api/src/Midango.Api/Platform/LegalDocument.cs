namespace Midango.Api.Platform;

/// The privacy policy or the terms, in both languages. Written in a small Markdown subset
/// (# / ## headings, paragraphs, "- " bullets, **bold**) that every client renders as text.
public sealed class LegalDocument
{
    public const int MaxBodyLength = 60_000;
    public static readonly string[] Kinds = ["privacy", "terms"];

    /// "privacy" or "terms".
    public required string Kind { get; init; }
    public required string BodyEn { get; set; }
    public required string BodyAr { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
