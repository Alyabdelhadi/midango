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
        durationsMinutes = (j['durationsMinutes'] as List).cast<int>(),
        playersPerMatch = j['playersPerMatch'] as int? ?? 4;
  final String id;
  @override
  final String nameEn;
  @override
  final String nameAr;
  final String iconKey;

  /// Uploaded icon ("/media/…"); wins over [iconKey] when set.
  final String? iconUrl;
  final List<int> durationsMinutes;

  /// The most players an open match of this sport can have (the operator's setting).
  final int playersPerMatch;
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
        cancelledByVenue = j['cancelledByVenue'] as bool,
        role = j['role'] as String? ?? 'organiser',
        match = j['match'] == null ? null : BookingMatch.fromJson(j['match'] as JsonMap),
        myShare = j['myShare'] == null ? null : _money(j['myShare']),
        playerStatus = j['playerStatus'] as String?,
        pointsRedeemed = j['pointsRedeemed'] as int? ?? 0,
        pointsDiscount = j['pointsDiscount'] == null ? 0 : _money(j['pointsDiscount']),
        amountToPay = j['amountToPay'] == null ? null : _money(j['amountToPay']);
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

  /// "organiser" (the customer booked it) or "player" (joined someone's open match).
  final String role;

  /// Set while the booking is open to other players.
  final BookingMatch? match;

  /// What this customer pays at the desk when in a match.
  final double? myShare;

  /// A player's place: "joined" | "left" | "removed"; null for the organiser.
  final String? playerStatus;

  /// Points this customer spent on this booking, and the money they took off.
  final int pointsRedeemed;
  final double pointsDiscount;

  /// What this customer pays at the desk after their points discount.
  final double? amountToPay;

  bool get isPlayer => role == 'player';

  /// This customer's own amount before points: their share in a match, else the total.
  double get ownAmount => myShare ?? total;
  double get toPay => amountToPay ?? ownAmount - pointsDiscount;

  String venueName(bool arabic) => _pick(arabic, venueNameAr, venueNameEn)!;
  String courtName(bool arabic) => _pick(arabic, courtNameAr, courtNameEn)!;
  bool get confirmed => status == 'Confirmed';

  /// Still ahead and still on: shows the QR code.
  bool get upcoming => confirmed && DateTime.parse(endsAt).isAfter(DateTime.now());

  /// The API refuses to cancel once the start has passed.
  bool get cancellable => confirmed && DateTime.parse(startsAt).isAfter(DateTime.now());
  bool get freeToCancel => DateTime.now().isBefore(DateTime.parse(freeCancellationUntil));
}

class BookingMatch {
  BookingMatch.fromJson(JsonMap j)
      : maxPlayers = j['maxPlayers'] as int,
        level = j['level'] as String?,
        share = _money(j['share']),
        players = _list(j['players'], MatchPlayer.fromJson);
  final int maxPlayers;

  /// "Beginner" | "Intermediate" | "Advanced", or null for any level.
  final String? level;
  final double share;
  final List<MatchPlayer> players;
}

class MatchPlayer {
  MatchPlayer.fromJson(JsonMap j)
      : customerId = j['customerId'] as String?,
        firstName = j['firstName'] as String;
  final String? customerId;
  final String firstName;
}

/// An open match as others see it (list, detail, join and leave all answer with this).
class OpenMatch {
  OpenMatch.fromJson(JsonMap j)
      : id = j['id'] as String,
        venueId = j['venueId'] as String,
        categoryId = j['categoryId'] as String,
        venueNameEn = j['venueNameEn'] as String,
        venueNameAr = j['venueNameAr'] as String,
        venueLogoUrl = j['venueLogoUrl'] as String?,
        courtNameEn = j['courtNameEn'] as String,
        courtNameAr = j['courtNameAr'] as String,
        startsAt = j['startsAt'] as String,
        endsAt = j['endsAt'] as String,
        minutes = j['minutes'] as int,
        currency = j['currency'] as String,
        share = _money(j['share']),
        maxPlayers = j['maxPlayers'] as int,
        spotsLeft = j['spotsLeft'] as int,
        level = j['level'] as String?,
        players = _list(j['players'], MatchPlayer.fromJson),
        organiserFirstName = j['organiserFirstName'] as String?,
        addressEn = j['addressEn'] as String?,
        addressAr = j['addressAr'] as String?,
        freeCancellationUntil = j['freeCancellationUntil'] as String?;
  final String id;
  final String venueId;
  final String categoryId;
  final String venueNameEn;
  final String venueNameAr;
  final String? venueLogoUrl;
  final String courtNameEn;
  final String courtNameAr;
  final String startsAt;
  final String endsAt;
  final int minutes;
  final String currency;
  final double share;
  final int maxPlayers;
  final int spotsLeft;
  final String? level;

  /// Organiser first.
  final List<MatchPlayer> players;
  final String? organiserFirstName;
  final String? addressEn;
  final String? addressAr;
  final String? freeCancellationUntil;

  String venueName(bool arabic) => _pick(arabic, venueNameAr, venueNameEn)!;
  String courtName(bool arabic) => _pick(arabic, courtNameAr, courtNameEn)!;
  String? address(bool arabic) => _pick(arabic, addressAr, addressEn);
}

/// The equal share of a court's total, rounded like the API (2 places, halves away from zero).
double shareOf(double total, int players) => (total / players * 100).round() / 100;

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

/// The customer's points: balance, tier and the redeem rules (`GET /me/points`).
class Points {
  Points.fromJson(JsonMap j)
      : balance = j['balance'] as int,
        tier = j['tier'] as String,
        gamesInWindow = j['gamesInWindow'] as int,
        nextTierName = (j['nextTier'] as JsonMap?)?['name'] as String?,
        nextTierGames = (j['nextTier'] as JsonMap?)?['gamesNeeded'] as int?,
        multiplier = _money(j['multiplier']),
        pointsPerStep = (j['redeem'] as JsonMap)['pointsPerStep'] as int,
        sarPerStep = _money((j['redeem'] as JsonMap)['sarPerStep']),
        maxPercent = _money((j['redeem'] as JsonMap)['maxPercent']),
        history = _list(j['history'], PointsEntry.fromJson);
  final int balance;

  /// "Bronze" | "Silver" | "Gold"
  final String tier;
  final int gamesInWindow;

  /// Null at the top tier.
  final String? nextTierName;
  final int? nextTierGames;
  final double multiplier;
  final int pointsPerStep;
  final double sarPerStep;
  final double maxPercent;
  final List<PointsEntry> history;
}

class PointsEntry {
  PointsEntry.fromJson(JsonMap j)
      : at = j['at'] as String,
        points = j['points'] as int,
        kind = j['kind'] as String,
        bookingId = j['bookingId'] as String?,
        venueNameEn = j['venueNameEn'] as String?,
        venueNameAr = j['venueNameAr'] as String?;
  final String at;
  final int points;

  /// "Earn" | "Redeem" | "EarnReversal" | "RedeemReleased"
  final String kind;
  final String? bookingId;
  final String? venueNameEn;
  final String? venueNameAr;

  String? venueName(bool arabic) => _pick(arabic, venueNameAr, venueNameEn);
}

/// A tournament as players see it. The list sends the summary; the page (`GET /tournaments/{id}`) adds
/// the description, address, courts and the registered teams.
class Tournament implements Named {
  Tournament.fromJson(JsonMap j)
      : id = j['id'] as String,
        venueId = j['venueId'] as String,
        venueNameEn = j['venueNameEn'] as String,
        venueNameAr = j['venueNameAr'] as String,
        venueLogoUrl = j['venueLogoUrl'] as String?,
        categoryId = j['categoryId'] as String,
        nameEn = j['nameEn'] as String,
        nameAr = j['nameAr'] as String,
        format = j['format'] as String,
        teamSize = j['teamSize'] as int,
        startsAt = j['startsAt'] as String,
        endsAt = j['endsAt'] as String,
        registrationClosesAt = j['registrationClosesAt'] as String,
        entryFee = _money(j['entryFee']),
        currency = j['currency'] as String,
        maxTeams = j['maxTeams'] as int,
        registered = j['registered'] as int,
        waitlisted = j['waitlisted'] as int? ?? 0,
        level = j['level'] as String?,
        registrationOpen = j['registrationOpen'] as bool,
        descriptionEn = j['descriptionEn'] as String?,
        descriptionAr = j['descriptionAr'] as String?,
        addressEn = j['addressEn'] as String?,
        addressAr = j['addressAr'] as String?,
        courts = _list(j['courts'], TournamentCourt.fromJson),
        teams = _list(j['teams'], TournamentTeam.fromJson);
  final String id;
  final String venueId;
  final String venueNameEn;
  final String venueNameAr;
  final String? venueLogoUrl;
  final String categoryId;
  @override
  final String nameEn;
  @override
  final String nameAr;

  /// "Knockout" | "RoundRobin"
  final String format;
  final int teamSize;
  final String startsAt;
  final String endsAt;
  final String registrationClosesAt;
  final double entryFee;
  final String currency;
  final int maxTeams;
  final int registered;
  final int waitlisted;

  /// "Beginner" | "Intermediate" | "Advanced", or null for any level.
  final String? level;
  final bool registrationOpen;
  final String? descriptionEn;
  final String? descriptionAr;
  final String? addressEn;
  final String? addressAr;
  final List<TournamentCourt> courts;

  /// Registered teams only (the page; empty in the list).
  final List<TournamentTeam> teams;

  String venueName(bool arabic) => _pick(arabic, venueNameAr, venueNameEn)!;
  String? description(bool arabic) => _pick(arabic, descriptionAr, descriptionEn);
  String? address(bool arabic) => _pick(arabic, addressAr, addressEn);
  bool get started => !DateTime.parse(startsAt).isAfter(DateTime.now());
}

class TournamentCourt implements Named {
  TournamentCourt.fromJson(JsonMap j)
      : nameEn = j['nameEn'] as String,
        nameAr = j['nameAr'] as String;
  @override
  final String nameEn;
  @override
  final String nameAr;
}

/// A team: on the tournament page just the name and first names; the captain's own team also has its id,
/// status and check-in.
class TournamentTeam {
  TournamentTeam.fromJson(JsonMap j)
      : id = j['id'] as String?,
        name = j['name'] as String,
        status = j['status'] as String?,
        members = (j['members'] as List? ?? const []).cast<String>(),
        checkedInAt = j['checkedInAt'] as String?;
  final String? id;
  final String name;

  /// "Registered" | "Waitlisted" | "Withdrawn"
  final String? status;

  /// Captain first.
  final List<String> members;
  final String? checkedInAt;

  bool get withdrawn => status == 'Withdrawn';
  bool get waitlisted => status == 'Waitlisted';
}

/// A team the customer captains, with its tournament (`GET /me/tournaments`, and what register and withdraw
/// answer).
class MyTournament {
  MyTournament.fromJson(JsonMap j)
      : tournament = Tournament.fromJson(j['tournament'] as JsonMap),
        team = TournamentTeam.fromJson(j['team'] as JsonMap);
  final Tournament tournament;
  final TournamentTeam team;
}
