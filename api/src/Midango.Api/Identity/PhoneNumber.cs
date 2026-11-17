using PhoneNumbers;

namespace Midango.Api.Identity;

/// A mobile number normalised to E.164. Anything shown to staff or written to logs uses Masked.
public readonly record struct PhoneNumber
{
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    private PhoneNumber(string e164) => E164 = e164;

    public string E164 { get; }

    /// "•••• 4567": the only form venue staff ever see (decision Q2).
    public string Masked => Mask(E164);

    public static string Mask(string e164) => $"•••• {e164[^4..]}";

    /// Accepts local ("0551234567") or international ("+966551234567") input; Saudi Arabia is the default region.
    public static bool TryParse(string? input, out PhoneNumber phone)
    {
        phone = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        try
        {
            var parsed = Util.Parse(input, "SA");
            var type = Util.GetNumberType(parsed);
            if (!Util.IsValidNumber(parsed) || type is not (PhoneNumberType.MOBILE or PhoneNumberType.FIXED_LINE_OR_MOBILE))
                return false;
            phone = new PhoneNumber(Util.Format(parsed, PhoneNumberFormat.E164));
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    /// Never print the full number by accident.
    public override string ToString() => Masked;
}
