namespace Midango.Api.Catalog;

public static class Input
{
    /// Both languages present and within the limit. The app is bilingual, so a missing
    /// translation would show as a blank label.
    public static bool Bilingual(string? en, string? ar, int maxLength) =>
        !string.IsNullOrWhiteSpace(en) && !string.IsNullOrWhiteSpace(ar)
        && en.Trim().Length <= maxLength && ar.Trim().Length <= maxLength;
}
