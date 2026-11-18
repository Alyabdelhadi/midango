/// Plain immutable views of the API's JSON. Times stay ISO strings carrying the venue's offset.
library;

/// Anything with an English and an Arabic name.
abstract interface class Named {
  String get nameEn;
  String get nameAr;
}

extension NamedIn on Named {
  String name(bool arabic) => arabic && nameAr.isNotEmpty ? nameAr : nameEn;
}

typedef JsonMap = Map<String, dynamic>;

List<T> _list<T>(Object? json, T Function(JsonMap) from) =>
    (json as List? ?? const []).map((e) => from(e as JsonMap)).toList();

double _money(Object? n) => (n as num).toDouble();

String? _pick(bool arabic, String? ar, String? en) => arabic && (ar ?? '').isNotEmpty ? ar : en;

class Category implements Named {
  Category.fromJson(JsonMap j)
      : id = j['id'] as String,
        nameEn = j['nameEn'] as String,
        nameAr = j['nameAr'] as String,
        iconKey = j['iconKey'] as String,
        iconUrl = j['iconUrl'] as String?,
        durationsMinutes = (j['durationsMinutes'] as List).cast<int>();
  final String id;
  @override
  final String nameEn;
  @override
  final String nameAr;
  final String iconKey;

  /// Uploaded icon ("/media/…"); wins over [iconKey] when set.
  final String? iconUrl;
  final List<int> durationsMinutes;
}

class SlotCourt {
  SlotCourt.fromJson(JsonMap j)
      : courtId = j['courtId'] as String,
        total = _money(j['total']);
  final String courtId;
  final double total;
}

/// One start time. An empty `courts` list means the time is full.
class Slot {
  Slot.fromJson(JsonMap j)
      : start = j['start'] as String,
        localTime = j['localTime'] as String,
        courts = _list(j['courts'], SlotCourt.fromJson);
  final String start;
  final String localTime;
  final List<SlotCourt> courts;
  bool get free => courts.isNotEmpty;
}

class VenueCard implements Named {
  VenueCard.fromJson(JsonMap j)
      : id = j['id'] as String,
        nameEn = j['nameEn'] as String,
        nameAr = j['nameAr'] as String,
        addressEn = j['addressEn'] as String?,
        addressAr = j['addressAr'] as String?,
        currency = j['currency'] as String,
        courts = j['courts'] as int,
        fromTotal = j['fromTotal'] == null ? null : _money(j['fromTotal']),
        open = j['open'] as bool,
        times = _list(j['times'], Slot.fromJson),
        logoUrl = j['logoUrl'] as String?,
        photoUrl = j['photoUrl'] as String?;
  final String id;
  @override
  final String nameEn;
  @override
  final String nameAr;
  final String? addressEn;
  final String? addressAr;
  final String currency;
  final int courts;
  final double? fromTotal;
  final bool open;
  final List<Slot> times;
  final String? logoUrl;

  /// The venue's first photo.
  final String? photoUrl;

  String? address(bool arabic) => _pick(arabic, addressAr, addressEn);
}

class Hours {
  Hours.fromJson(JsonMap j)
      : day = j['day'] as String,
        opens = (j['opens'] as String).substring(0, 5),
        closes = (j['closes'] as String).substring(0, 5);

  /// English day name, e.g. "Sunday".
  final String day;
  final String opens;
  final String closes;
}

class Feature implements Named {
  Feature.fromJson(JsonMap j)
      : nameEn = j['nameEn'] as String,
        nameAr = j['nameAr'] as String,
        iconUrl = j['iconUrl'] as String?;
  @override
  final String nameEn;
  @override
  final String nameAr;
  final String? iconUrl;
}

class Court implements Named {
  Court.fromJson(JsonMap j)
      : id = j['id'] as String,
        categoryId = j['categoryId'] as String,
        nameEn = j['nameEn'] as String,
        nameAr = j['nameAr'] as String,
        indoor = j['indoor'] as bool,
        features = _list(j['features'], Feature.fromJson);
  final String id;
  final String categoryId;
  @override
  final String nameEn;
  @override
  final String nameAr;
  final bool indoor;
  final List<Feature> features;
}

class VenueDetail implements Named {
  VenueDetail.fromJson(JsonMap j)
      : id = j['id'] as String,
        nameEn = j['nameEn'] as String,
        nameAr = j['nameAr'] as String,
        addressEn = j['addressEn'] as String?,
        addressAr = j['addressAr'] as String?,
        latitude = (j['latitude'] as num?)?.toDouble(),
        longitude = (j['longitude'] as num?)?.toDouble(),
        currency = j['currency'] as String,
        vatPercent = _money(j['vatPercent']),
        freeCancellationHours = j['freeCancellationHours'] as int,
        hours = _list(j['hours'], Hours.fromJson),
        categories = _list(j['categories'], Category.fromJson),
        courts = _list(j['courts'], Court.fromJson),
        logoUrl = j['logoUrl'] as String?,
        photos = (j['photos'] as List? ?? const []).cast<String>();
  final String id;
  @override
  final String nameEn;
  @override
  final String nameAr;
  final String? addressEn;
  final String? addressAr;
  final double? latitude;
  final double? longitude;
  final String currency;
  final double vatPercent;
  final int freeCancellationHours;
  final List<Hours> hours;
  final List<Category> categories;
  final List<Court> courts;
  final String? logoUrl;
  final List<String> photos;

  String? address(bool arabic) => _pick(arabic, addressAr, addressEn);
  Court? court(String id) => courts.where((c) => c.id == id).firstOrNull;
}

class Availability {
  Availability.fromJson(JsonMap j)
      : date = j['date'] as String,
        open = j['open'] as bool,
        currency = j['currency'] as String,
        slots = _list(j['slots'], Slot.fromJson);
  final String date;
  final bool open;
  final String currency;
  final List<Slot> slots;
}

class Booking {
  Booking.fromJson(JsonMap j)
      : id = j['id'] as String,
        reference = j['reference'] as String,
        venueId = j['venueId'] as String,
        venueNameEn = j['venueNameEn'] as String,
        venueNameAr = j['venueNameAr'] as String,
        courtId = j['courtId'] as String,
        courtNameEn = j['courtNameEn'] as String,
        courtNameAr = j['courtNameAr'] as String,
        startsAt = j['startsAt'] as String,
        endsAt = j['endsAt'] as String,
        minutes = j['minutes'] as int,
        total = _money(j['total']),
        net = _money(j['net']),
        vat = _money(j['vat']),
        currency = j['currency'] as String,
        paymentMethod = j['paymentMethod'] as String,
        status = j['status'] as String,
        freeCancellationUntil = j['freeCancellationUntil'] as String,
        lateCancellation = j['lateCancellation'] as bool,
        cancelledAt = j['cancelledAt'] as String?,
        cancelledByVenue = j['cancelledByVenue'] as bool;
  final String id;
  final String reference;
  final String venueId;
  final String venueNameEn;
  final String venueNameAr;
  final String courtId;
  final String courtNameEn;
  final String courtNameAr;
  final String startsAt;
  final String endsAt;
  final int minutes;
  final double total;
  final double net;
  final double vat;
  final String currency;
  final String paymentMethod;

  /// "Confirmed" | "Cancelled" | "NoShow"
  final String status;
  final String freeCancellationUntil;
  final bool lateCancellation;
  final String? cancelledAt;
  final bool cancelledByVenue;

  String venueName(bool arabic) => _pick(arabic, venueNameAr, venueNameEn)!;
  String courtName(bool arabic) => _pick(arabic, courtNameAr, courtNameEn)!;
  bool get confirmed => status == 'Confirmed';

  /// Still ahead and still on: shows the QR code.
  bool get upcoming => confirmed && DateTime.parse(endsAt).isAfter(DateTime.now());

  /// The API refuses to cancel once the start has passed.
  bool get cancellable => confirmed && DateTime.parse(startsAt).isAfter(DateTime.now());
  bool get freeToCancel => DateTime.now().isBefore(DateTime.parse(freeCancellationUntil));
}

class MyBookings {
  MyBookings.fromJson(JsonMap j)
      : upcoming = _list(j['upcoming'], Booking.fromJson),
        history = _list(j['history'], Booking.fromJson);
  final List<Booking> upcoming;
  final List<Booking> history;
}

class Me {
  Me.fromJson(JsonMap j)
      : id = j['id'] as String,
        phone = j['phone'] as String,
        name = j['name'] as String?;
  final String id;
  final String phone;
  final String? name;
}
