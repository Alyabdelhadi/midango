namespace Midango.Api.Catalog;

public sealed record PriceBandRule(DayOfWeek Day, TimeOnly From, TimeOnly To, decimal HourlyPrice);

public sealed record PriceSegment(TimeOnly From, TimeOnly To, decimal HourlyPrice, decimal Amount);

public sealed record PriceQuote(decimal Total, decimal Net, decimal Vat, IReadOnlyList<PriceSegment> Segments);

/// The one place a booking's price is worked out (price check now, bookings in M5).
public static class Pricing
{
    private const int MinutesPerDay = 1440;
    private const int MinutesPerWeek = 7 * MinutesPerDay;
    private const int SliceMinutes = 15;

    /// Prices `minutes` starting at `start` on `day`. Each 15-minute slice uses the period it starts in,
    /// or the court's base price outside every period. All prices are VAT-inclusive.
    public static PriceQuote Calculate(
        decimal basePrice, IReadOnlyList<PriceBandRule> bands, DayOfWeek day, TimeOnly start, int minutes, decimal vatPercent)
    {
        var startOfWeek = (int)day * MinutesPerDay + start.Hour * 60 + start.Minute;
        var segments = new List<PriceSegment>();

        for (var offset = 0; offset < minutes; offset += SliceMinutes)
        {
            var length = Math.Min(SliceMinutes, minutes - offset);
            var rate = RateAt((startOfWeek + offset) % MinutesPerWeek, basePrice, bands);
            var amount = rate * length / 60m;
            var to = start.AddMinutes(offset + length);

            if (segments.Count > 0 && segments[^1].HourlyPrice == rate)
                segments[^1] = segments[^1] with { To = to, Amount = segments[^1].Amount + amount };
            else
                segments.Add(new PriceSegment(start.AddMinutes(offset), to, rate, amount));
        }

        var total = Round(segments.Sum(s => s.Amount));
        var net = Round(total / (1 + vatPercent / 100m));
        return new PriceQuote(total, net, total - net, segments.Select(s => s with { Amount = Round(s.Amount) }).ToList());
    }

    /// Price per hour at a minute of the week (Sunday 00:00 = 0).
    public static decimal RateAt(int minuteOfWeek, decimal basePrice, IReadOnlyList<PriceBandRule> bands)
    {
        foreach (var band in bands)
        {
            var (start, length) = Span(band);
            if (Distance(start, minuteOfWeek) < length) return band.HourlyPrice;
        }
        return basePrice;
    }

    /// True when two periods share any minute, including periods that run past midnight.
    public static bool Overlaps(PriceBandRule a, PriceBandRule b)
    {
        var (startA, lengthA) = Span(a);
        var (startB, lengthB) = Span(b);
        return Distance(startA, startB) < lengthA || Distance(startB, startA) < lengthB;
    }

    public static bool ValidPrice(decimal price) => price is >= 1m and <= 10_000m && decimal.Round(price, 2) == price;

    public static bool OnQuarterHour(TimeOnly time) => time.Minute % SliceMinutes == 0 && time.Second == 0 && time.Millisecond == 0;

    /// Start minute of the week and length in minutes. To <= From wraps past midnight.
    private static (int Start, int Length) Span(PriceBandRule band)
    {
        var from = band.From.Hour * 60 + band.From.Minute;
        var to = band.To.Hour * 60 + band.To.Minute;
        var length = (to - from + MinutesPerDay) % MinutesPerDay;
        return ((int)band.Day * MinutesPerDay + from, length == 0 ? MinutesPerDay : length);
    }

    /// Minutes forward from `from` to `to` around the week.
    private static int Distance(int from, int to) => ((to - from) % MinutesPerWeek + MinutesPerWeek) % MinutesPerWeek;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
