import 'strings.dart';

/// Venue times arrive with the venue's offset; the wall-clock part is read straight from the string.
String clock(String iso) => iso.substring(11, 16);
String dayOf(String iso) => iso.substring(0, 10);

/// "20:00–21:30", isolated left-to-right so it doesn't reverse inside Arabic.
String timeRange(String startIso, String endIso) => ltr('${clock(startIso)}–${clock(endIso)}');

/// The range for a start time and a length, before a booking exists (wraps past midnight).
String timeRangeFor(String startIso, int minutes) {
  final start = clock(startIso);
  final total = int.parse(start.substring(0, 2)) * 60 + int.parse(start.substring(3, 5)) + minutes;
  String two(int n) => n.toString().padLeft(2, '0');
  return ltr('$start–${two(total ~/ 60 % 24)}:${two(total % 60)}');
}

/// "Thu 19 Nov" / "الخميس 19 نوفمبر" — Gregorian, Western digits.
String formatDay(String date, Strings s) {
  final d = DateTime.parse(date);
  return '${s.weekdays[d.weekday - 1]} ${d.day} ${s.months[d.month - 1]}';
}

/// "SAR 240.00" / "240.00 ر.س."
String money(double amount, String currency, Strings s) =>
    s.isArabic ? '${amount.toStringAsFixed(2)} ${currency == 'SAR' ? 'ر.س.' : currency}' : '$currency ${amount.toStringAsFixed(2)}';

String isoDate(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

/// 1.0 → "1", 1.25 → "1.25": multipliers and percentages without trailing zeros.
String plain(double v) => v == v.roundToDouble() ? v.toInt().toString() : v.toString();
