import 'package:flutter/widgets.dart';

/// Every visible string, in English and Arabic. A string missing from one language is a compile error.
class Strings {
  const Strings({
    required this.isArabic,
    required this.weekdays,
    required this.months,
    required this.tabPlay,
    required this.tabBookings,
    required this.tabProfile,
    required this.playTitle,
    required this.today,
    required this.tomorrow,
    required this.minutes,
    required this.fromPrice,
    required this.noOpenTimes,
    required this.closedThisDay,
    required this.noVenues,
    required this.noSports,
    required this.somethingWrong,
    required this.tryAgain,
    required this.slotOpen,
    required this.slotFull,
    required this.timesHeading,
    required this.courtsHeading,
    required this.hoursHeading,
    required this.closed,
    required this.indoor,
    required this.outdoor,
    required this.confirmTitle,
    required this.courtLabel,
    required this.totalLabel,
    required this.includesVat,
    required this.payAtVenue,
    required this.freeCancellationHours,
    required this.bookFor,
    required this.signIn,
    required this.phoneHeading,
    required this.phoneHelp,
    required this.phoneLabel,
    required this.phoneHint,
    required this.sendCode,
    required this.codeHeading,
    required this.codeSentTo,
    required this.codeLabel,
    required this.verify,
    required this.resendCode,
    required this.resendIn,
    required this.nameHeading,
    required this.nameHelp,
    required this.nameLabel,
    required this.save,
    required this.booked,
    required this.referenceLabel,
    required this.showAtDesk,
    required this.payAmountAtVenue,
    required this.freeCancellationUntil,
    required this.viewMyBookings,
    required this.view,
    required this.done,
    required this.bookingsTitle,
    required this.signInForBookings,
    required this.upcoming,
    required this.past,
    required this.noUpcoming,
    required this.findCourt,
    required this.statusConfirmed,
    required this.statusCancelled,
    required this.statusCancelledByVenue,
    required this.statusLateCancellation,
    required this.statusNoShow,
    required this.bookingTitle,
    required this.cancelBooking,
    required this.cancelTitle,
    required this.cancelFree,
    required this.cancelLate,
    required this.keepBooking,
    required this.bookingCancelled,
    required this.profileTitle,
    required this.signInForProfile,
    required this.nameTitle,
    required this.addName,
    required this.phoneTitle,
    required this.language,
    required this.english,
    required this.arabic,
    required this.signOut,
    required this.privacyPolicy,
    required this.termsConditions,
    required this.lastUpdated,
    required this.openMatches,
    required this.noMatches,
    required this.levelAny,
    required this.levelName,
    required this.levelLabel,
    required this.spotsLeft,
    required this.perPlayer,
    required this.matchTitle,
    required this.playersHeading,
    required this.organiser,
    required this.you,
    required this.sharePayAtDesk,
    required this.freeLeaveUntil,
    required this.joinMatch,
    required this.joinAnyway,
    required this.matchFull,
    required this.levelMismatch,
    required this.joined,
    required this.leaveMatch,
    required this.leaveTitle,
    required this.leaveFree,
    required this.leaveLate,
    required this.stayIn,
    required this.leftMatch,
    required this.yourMatch,
    required this.viewBooking,
    required this.openToOthers,
    required this.openToOthersHelp,
    required this.yourShare,
    required this.openMatch,
    required this.saveChanges,
    required this.closeMatch,
    required this.closeBlocked,
    required this.remove,
    required this.removeTitle,
    required this.notNow,
    required this.matchOpened,
    required this.matchClosed,
    required this.matchPill,
    required this.statusLeft,
    required this.statusRemoved,
    required this.yourLevel,
    required this.yourLevelHelp,
    required this.notSet,
    required this.playerRemoved,
    required this.fewerPlayers,
    required this.morePlayers,
    required this.pointsTitle,
    required this.pointsCount,
    required this.tierName,
    required this.gamesToNext,
    required this.topTier,
    required this.earnMultiplier,
    required this.pointsHistory,
    required this.noPointsYet,
    required this.pointsKind,
    required this.usePoints,
    required this.usePointsHelp,
    required this.pointsHave,
    required this.notEnoughPoints,
    required this.fewerPoints,
    required this.morePoints,
    required this.pointsOffYouPay,
    required this.pointsApplied,
    required this.pointsRemoved,
    required this.withPoints,
    required this.tournaments,
    required this.noTournaments,
    required this.tournamentTitle,
    required this.formatName,
    required this.formatHelp,
    required this.teamSize,
    required this.teamsCount,
    required this.onWaitlist,
    required this.registrationCloses,
    required this.registrationClosed,
    required this.perTeam,
    required this.feePerTeam,
    required this.freeEntry,
    required this.teamsHeading,
    required this.noTeamsYet,
    required this.registerTeam,
    required this.register,
    required this.teamNameLabel,
    required this.teammateLabel,
    required this.captain,
    required this.teammatesHelp,
    required this.requiredField,
    required this.teamRegistered,
    required this.teamWaitlisted,
    required this.yourTeam,
    required this.statusRegistered,
    required this.statusWaitlist,
    required this.statusCheckedIn,
    required this.statusWithdrawn,
    required this.withdrawTeam,
    required this.withdrawTitle,
    required this.withdrawBody,
    required this.keepTeam,
    required this.teamWithdrawn,
    required this.tabInfo,
    required this.tabDraw,
    required this.tabFixtures,
    required this.knockoutRound,
    required this.groupRound,
    required this.groupName,
    required this.teamColumn,
    required this.standingsColumns,
    required this.yourNextMatch,
    required this.bye,
    required this.toBeDecided,
    required this.champion,
    required this.nextMatchLine,
  });

  final bool isArabic;

  /// Monday first, 7 entries.
  final List<String> weekdays;
  final List<String> months;

  // Shell
  final String tabPlay;
  final String tabBookings;
  final String tabProfile;

  // Play
  final String playTitle;
  final String today;
  final String tomorrow;
  final String Function(int n) minutes;
  final String Function(String price) fromPrice;
  final String noOpenTimes;
  final String closedThisDay;
  final String noVenues;
  final String noSports;
  final String somethingWrong;
  final String tryAgain;
  final String Function(String time) slotOpen;
  final String Function(String time) slotFull;

  // Venue page
  final String timesHeading;
  final String courtsHeading;
  final String hoursHeading;
  final String closed;
  final String indoor;
  final String outdoor;

  // Confirm sheet
  final String confirmTitle;
  final String courtLabel;
  final String totalLabel;
  final String includesVat;
  final String payAtVenue;
  final String Function(int hours) freeCancellationHours;
  final String Function(String price) bookFor;

  // Sign in
  final String signIn;
  final String phoneHeading;
  final String phoneHelp;
  final String phoneLabel;
  final String phoneHint;
  final String sendCode;
  final String codeHeading;
  final String Function(String phone) codeSentTo;
  final String codeLabel;
  final String verify;
  final String resendCode;
  final String Function(int seconds) resendIn;
  final String nameHeading;
  final String nameHelp;
  final String nameLabel;
  final String save;

  // Confirmation
  final String booked;
  final String referenceLabel;
  final String showAtDesk;
  final String Function(String price) payAmountAtVenue;
  final String Function(String when) freeCancellationUntil;
  final String viewMyBookings;
  final String view;
  final String done;

  // Bookings
  final String bookingsTitle;
  final String signInForBookings;
  final String upcoming;
  final String past;
  final String noUpcoming;
  final String findCourt;
  final String statusConfirmed;
  final String statusCancelled;
  final String statusCancelledByVenue;
  final String statusLateCancellation;
  final String statusNoShow;
  final String bookingTitle;
  final String cancelBooking;
  final String cancelTitle;
  final String cancelFree;
  final String cancelLate;
  final String keepBooking;
  final String bookingCancelled;

  // Profile
  final String profileTitle;
  final String signInForProfile;
  final String nameTitle;
  final String addName;
  final String phoneTitle;
  final String language;
  final String english;
  final String arabic;
  final String signOut;
  final String privacyPolicy;
  final String termsConditions;
  final String Function(String date) lastUpdated;

  // Open matches
  final String openMatches;
  final String noMatches;
  final String levelAny;
  final String Function(String level) levelName;
  final String levelLabel;
  final String Function(int n) spotsLeft;
  final String perPlayer;
  final String matchTitle;
  final String playersHeading;
  final String organiser;
  final String you;
  final String Function(String price) sharePayAtDesk;
  final String Function(String when) freeLeaveUntil;
  final String joinMatch;
  final String joinAnyway;
  final String matchFull;
  final String Function(String level) levelMismatch;
  final String joined;
  final String leaveMatch;
  final String leaveTitle;
  final String leaveFree;
  final String leaveLate;
  final String stayIn;
  final String leftMatch;
  final String yourMatch;
  final String viewBooking;
  final String openToOthers;
  final String openToOthersHelp;
  final String yourShare;
  final String openMatch;
  final String saveChanges;
  final String closeMatch;
  final String closeBlocked;
  final String remove;
  final String Function(String name) removeTitle;
  final String notNow;
  final String matchOpened;
  final String matchClosed;
  final String matchPill;
  final String statusLeft;
  final String statusRemoved;
  final String yourLevel;
  final String yourLevelHelp;
  final String notSet;
  final String playerRemoved;
  final String fewerPlayers;
  final String morePlayers;

  // Points
  final String pointsTitle;
  final String Function(int n) pointsCount;

  /// "Bronze" | "Silver" | "Gold" in words.
  final String Function(String tier) tierName;
  final String Function(int games, String tier) gamesToNext;
  final String topTier;
  final String Function(String multiplier) earnMultiplier;
  final String pointsHistory;
  final String noPointsYet;

  /// "Earn" | "Redeem" | "EarnReversal" | "RedeemReleased" in words.
  final String Function(String kind) pointsKind;
  final String usePoints;
  final String Function(int points, String off, String percent) usePointsHelp;
  final String Function(int n) pointsHave;
  final String notEnoughPoints;
  final String fewerPoints;
  final String morePoints;
  final String Function(String off, String pay) pointsOffYouPay;
  final String pointsApplied;
  final String pointsRemoved;
  final String Function(String off) withPoints;

  // Tournaments
  final String tournaments;
  final String noTournaments;
  final String tournamentTitle;
  final String Function(String format) formatName;
  final String Function(String format) formatHelp;
  final String Function(int n) teamSize;
  final String Function(int registered, int max) teamsCount;
  final String Function(int n) onWaitlist;
  final String Function(String when) registrationCloses;
  final String registrationClosed;
  final String perTeam;
  final String Function(String price) feePerTeam;
  final String freeEntry;
  final String teamsHeading;
  final String noTeamsYet;
  final String registerTeam;
  final String register;
  final String teamNameLabel;
  final String Function(int n) teammateLabel;
  final String captain;
  final String teammatesHelp;
  final String requiredField;
  final String teamRegistered;
  final String teamWaitlisted;
  final String Function(String name) yourTeam;
  final String statusRegistered;
  final String statusWaitlist;
  final String statusCheckedIn;
  final String statusWithdrawn;
  final String withdrawTeam;
  final String withdrawTitle;
  final String withdrawBody;
  final String keepTeam;
  final String teamWithdrawn;

  // Tournament draw and fixtures
  final String tabInfo;
  final String tabDraw;
  final String tabFixtures;

  /// Named by distance from the final: Final, Semi-finals, Quarter-finals, else Round n.
  final String Function(int round, int rounds) knockoutRound;
  final String Function(int round) groupRound;
  final String Function(int groupNo) groupName;
  final String teamColumn;

  /// Played, won, drawn, lost, score difference, points.
  final List<String> standingsColumns;
  final String yourNextMatch;
  final String bye;
  final String toBeDecided;
  final String champion;
  final String Function(String when, String? court) nextMatchLine;

  static Strings of(BuildContext context) => context.dependOnInheritedWidgetOfExactType<StringsScope>()!.strings;
}

/// Keeps a run of Latin text (phone numbers, time ranges) left-to-right inside Arabic.
String ltr(String text) => '${String.fromCharCode(0x2066)}$text${String.fromCharCode(0x2069)}';

/// Arabic counted noun for hours: 1 ساعة واحدة, 2 ساعتين, 3–10 ساعات, 11+ ساعة.
String _arHours(int n) => switch (n) {
      1 => 'ساعة واحدة',
      2 => 'ساعتين',
      >= 3 && <= 10 => '$n ساعات',
      _ => '$n ساعة',
    };

String _arMinutes(int n) => n >= 3 && n <= 10 ? '$n دقائق' : '$n دقيقة';

const en = Strings(
  isArabic: false,
  weekdays: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
  months: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
  tabPlay: 'Play',
  tabBookings: 'Bookings',
  tabProfile: 'Profile',
  playTitle: 'Play',
  today: 'Today',
  tomorrow: 'Tomorrow',
  minutes: _enMinutes,
  fromPrice: _enFrom,
  noOpenTimes: 'No open times on this day.',
  closedThisDay: 'Closed on this day.',
  noVenues: 'No venues for this sport yet.',
  noSports: 'No sports to book yet.',
  somethingWrong: 'Something went wrong. Try again.',
  tryAgain: 'Try again',
  slotOpen: _enSlotOpen,
  slotFull: _enSlotFull,
  timesHeading: 'Times',
  courtsHeading: 'Courts',
  hoursHeading: 'Opening hours',
  closed: 'Closed',
  indoor: 'Indoor',
  outdoor: 'Outdoor',
  confirmTitle: 'Confirm your booking',
  courtLabel: 'Court',
  totalLabel: 'Total',
  includesVat: 'Includes VAT',
  payAtVenue: 'Pay at the venue, in cash',
  freeCancellationHours: _enFreeHours,
  bookFor: _enBookFor,
  signIn: 'Sign in',
  phoneHeading: 'Your phone number',
  phoneHelp: "We'll text you a 6-digit code.",
  phoneLabel: 'Phone number',
  phoneHint: '05x xxx xxxx',
  sendCode: 'Send code',
  codeHeading: 'Enter the code',
  codeSentTo: _enCodeSentTo,
  codeLabel: '6-digit code',
  verify: 'Verify',
  resendCode: 'Resend code',
  resendIn: _enResendIn,
  nameHeading: 'What should the venue call you?',
  nameHelp: 'The desk uses it to find your booking.',
  nameLabel: 'Your name',
  save: 'Save',
  booked: "You're booked",
  referenceLabel: 'Booking reference',
  showAtDesk: 'Show this code at the desk',
  payAmountAtVenue: _enPayAmount,
  freeCancellationUntil: _enFreeUntil,
  viewMyBookings: 'View my bookings',
  view: 'View',
  done: 'Done',
  bookingsTitle: 'My bookings',
  signInForBookings: 'Sign in to see your bookings.',
  upcoming: 'Upcoming',
  past: 'Past',
  noUpcoming: 'No upcoming bookings.',
  findCourt: 'Find a court',
  statusConfirmed: 'Confirmed',
  statusCancelled: 'Cancelled',
  statusCancelledByVenue: 'Cancelled by the venue',
  statusLateCancellation: 'Late cancellation',
  statusNoShow: 'No-show',
  bookingTitle: 'Booking',
  cancelBooking: 'Cancel booking',
  cancelTitle: 'Cancel this booking?',
  cancelFree: 'Cancelling now is free.',
  cancelLate: 'Cancelling now counts as a late cancellation.',
  keepBooking: 'Keep booking',
  bookingCancelled: 'Booking cancelled.',
  profileTitle: 'Profile',
  signInForProfile: 'Sign in to book courts and see your bookings.',
  nameTitle: 'Name',
  addName: 'Add your name',
  phoneTitle: 'Phone',
  language: 'Language',
  english: 'English',
  arabic: 'العربية',
  signOut: 'Sign out',
  privacyPolicy: 'Privacy policy',
  termsConditions: 'Terms & conditions',
  lastUpdated: _enLastUpdated,
  openMatches: 'Open matches',
  noMatches: 'No open matches for this sport on this day.',
  levelAny: 'Any level',
  levelName: _enLevel,
  levelLabel: 'Level',
  spotsLeft: _enSpotsLeft,
  perPlayer: 'per player',
  matchTitle: 'Open match',
  playersHeading: 'Players',
  organiser: 'Organiser',
  you: 'You',
  sharePayAtDesk: _enShareAtDesk,
  freeLeaveUntil: _enFreeLeave,
  joinMatch: 'Join match',
  joinAnyway: 'Join anyway',
  matchFull: 'This match is full',
  levelMismatch: _enMismatch,
  joined: 'You joined the match.',
  leaveMatch: 'Leave match',
  leaveTitle: 'Leave this match?',
  leaveFree: 'Leaving now is free.',
  leaveLate: 'Leaving now counts as a late cancellation.',
  stayIn: 'Stay in',
  leftMatch: 'You left the match.',
  yourMatch: 'You organise this match.',
  viewBooking: 'View booking',
  openToOthers: 'Open to other players',
  openToOthersHelp: 'Others can join and pay an equal share at the desk. You cover any empty spots.',
  yourShare: 'Your share',
  openMatch: 'Open match',
  saveChanges: 'Save changes',
  closeMatch: 'Close match',
  closeBlocked: 'Remove the players who joined first.',
  remove: 'Remove',
  removeTitle: _enRemoveTitle,
  notNow: 'Not now',
  matchOpened: 'Your match is open to other players.',
  matchClosed: 'Match closed.',
  matchPill: 'Match',
  statusLeft: 'You left',
  statusRemoved: 'Removed by the organiser',
  yourLevel: 'Your level',
  yourLevelHelp: 'Tap your level again to clear it. Open matches start filtered to it.',
  notSet: 'Not set',
  playerRemoved: 'Player removed.',
  fewerPlayers: 'Fewer players',
  morePlayers: 'More players',
  pointsTitle: 'Your points',
  pointsCount: _enPoints,
  tierName: _enTier,
  gamesToNext: _enGamesToNext,
  topTier: "You're at the top tier.",
  earnMultiplier: _enEarn,
  pointsHistory: 'Points history',
  noPointsYet: 'No points yet. You earn them when you check in at the desk.',
  pointsKind: _enKind,
  usePoints: 'Use points',
  usePointsHelp: _enUseHelp,
  pointsHave: _enHave,
  notEnoughPoints: "You don't have enough points to use on this booking yet.",
  fewerPoints: 'Fewer points',
  morePoints: 'More points',
  pointsOffYouPay: _enOffPay,
  pointsApplied: 'Points applied.',
  pointsRemoved: 'Points removed.',
  withPoints: _enWithPoints,
  tournaments: 'Tournaments',
  noTournaments: 'No upcoming tournaments for this sport.',
  tournamentTitle: 'Tournament',
  formatName: _enFormat,
  formatHelp: _enFormatHelp,
  teamSize: _enTeamSize,
  teamsCount: _enTeamsCount,
  onWaitlist: _enOnWaitlist,
  registrationCloses: _enRegCloses,
  registrationClosed: 'Registration closed',
  perTeam: 'per team',
  feePerTeam: _enFeePerTeam,
  freeEntry: 'Free entry',
  teamsHeading: 'Teams',
  noTeamsYet: 'No teams yet. Be the first.',
  registerTeam: 'Register a team',
  register: 'Register',
  teamNameLabel: 'Team name',
  teammateLabel: _enTeammate,
  captain: 'Captain',
  teammatesHelp: "Type your teammates' names as they should appear in the tournament.",
  requiredField: 'Required',
  teamRegistered: 'Your team is in.',
  teamWaitlisted: "The tournament is full, so you're on the waitlist. We'll let you know if a place opens.",
  yourTeam: _enYourTeam,
  statusRegistered: 'Registered',
  statusWaitlist: 'Waitlist',
  statusCheckedIn: 'Checked in',
  statusWithdrawn: 'Withdrawn',
  withdrawTeam: 'Withdraw team',
  withdrawTitle: 'Withdraw your team?',
  withdrawBody: 'Your team gives up its place. You can register again while registration is open.',
  keepTeam: 'Keep my team',
  teamWithdrawn: 'Your team was withdrawn.',
  tabInfo: 'Info',
  tabDraw: 'Draw',
  tabFixtures: 'Fixtures',
  knockoutRound: _enKnockoutRound,
  groupRound: _enGroupRound,
  groupName: _enGroupName,
  teamColumn: 'Team',
  standingsColumns: ['P', 'W', 'D', 'L', '+/-', 'Pts'],
  yourNextMatch: 'Your next match',
  bye: 'Bye',
  toBeDecided: 'To be decided',
  champion: 'Champions',
  nextMatchLine: _enNextMatch,
);

String _enPoints(int n) => n == 1 ? '1 point' : '$n points';
String _enTier(String tier) => tier; // the API's tier names are the English words
String _enGamesToNext(int games, String tier) => '$games more ${games == 1 ? 'game' : 'games'} to $tier';
String _enEarn(String m) => 'You earn points ${ltr('×$m')}';
String _enKind(String kind) => switch (kind) {
      'Earn' => 'Earned',
      'Redeem' => 'Used on a booking',
      'EarnReversal' => 'Check-in undone',
      'RedeemReleased' => 'Returned to you',
      _ => kind,
    };
String _enUseHelp(int points, String off, String percent) =>
    '${_enPoints(points)} = $off off, up to $percent% of your amount.';
String _enHave(int n) => 'You have ${_enPoints(n)}';
String _enOffPay(String off, String pay) => '$off off · you pay $pay at the desk';
String _enWithPoints(String off) => '$off with points';

String _enLastUpdated(String date) => 'Last updated $date';
String _enLevel(String level) => level; // the API's band names are the English words
String _enSpotsLeft(int n) => n == 1 ? '1 spot left' : '$n spots left';
String _enShareAtDesk(String price) => 'Your share: $price, paid at the desk';
String _enFreeLeave(String when) => 'Free to leave until $when';
String _enMismatch(String level) => 'This match is for $level players';
String _enRemoveTitle(String name) => 'Remove $name from the match?';
String _enMinutes(int n) => '$n min';
String _enFrom(String price) => 'from $price';
String _enSlotOpen(String time) => '$time, available';
String _enSlotFull(String time) => '$time, full';
String _enFreeHours(int h) => 'Free cancellation up to $h ${h == 1 ? 'hour' : 'hours'} before the start';
String _enBookFor(String price) => 'Book · $price';
String _enCodeSentTo(String phone) => 'We sent a 6-digit code to $phone.';
String _enResendIn(int s) => 'Resend code in ${s}s';
String _enPayAmount(String price) => 'Pay $price at the venue, in cash';
String _enFreeUntil(String when) => 'Free cancellation until $when';

const ar = Strings(
  isArabic: true,
  weekdays: ['الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت', 'الأحد'],
  months: ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو', 'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر'],
  tabPlay: 'العب',
  tabBookings: 'الحجوزات',
  tabProfile: 'حسابي',
  playTitle: 'العب',
  today: 'اليوم',
  tomorrow: 'غداً',
  minutes: _arMinutes,
  fromPrice: _arFrom,
  noOpenTimes: 'لا توجد أوقات متاحة في هذا اليوم.',
  closedThisDay: 'مغلق في هذا اليوم.',
  noVenues: 'لا توجد منشآت لهذه الرياضة بعد.',
  noSports: 'لا توجد رياضات متاحة للحجز بعد.',
  somethingWrong: 'حدث خطأ ما. حاول مرة أخرى.',
  tryAgain: 'حاول مرة أخرى',
  slotOpen: _arSlotOpen,
  slotFull: _arSlotFull,
  timesHeading: 'الأوقات',
  courtsHeading: 'الملاعب',
  hoursHeading: 'ساعات العمل',
  closed: 'مغلق',
  indoor: 'داخلي',
  outdoor: 'خارجي',
  confirmTitle: 'تأكيد الحجز',
  courtLabel: 'الملعب',
  totalLabel: 'المجموع',
  includesVat: 'شامل ضريبة القيمة المضافة',
  payAtVenue: 'الدفع نقداً في المنشأة',
  freeCancellationHours: _arFreeHours,
  bookFor: _arBookFor,
  signIn: 'تسجيل الدخول',
  phoneHeading: 'رقم جوالك',
  phoneHelp: 'سنرسل إليك رمزاً من 6 أرقام في رسالة نصية.',
  phoneLabel: 'رقم الجوال',
  phoneHint: '05x xxx xxxx',
  sendCode: 'إرسال الرمز',
  codeHeading: 'أدخل الرمز',
  codeSentTo: _arCodeSentTo,
  codeLabel: 'الرمز المكوّن من 6 أرقام',
  verify: 'تحقّق',
  resendCode: 'إعادة إرسال الرمز',
  resendIn: _arResendIn,
  nameHeading: 'بأي اسم تناديك المنشأة؟',
  nameHelp: 'يستخدمه موظف الاستقبال للعثور على حجزك.',
  nameLabel: 'اسمك',
  save: 'حفظ',
  booked: 'تم الحجز',
  referenceLabel: 'رقم الحجز',
  showAtDesk: 'اعرض هذا الرمز في الاستقبال',
  payAmountAtVenue: _arPayAmount,
  freeCancellationUntil: _arFreeUntil,
  viewMyBookings: 'عرض حجوزاتي',
  view: 'عرض',
  done: 'تم',
  bookingsTitle: 'حجوزاتي',
  signInForBookings: 'سجّل الدخول لعرض حجوزاتك.',
  upcoming: 'القادمة',
  past: 'السابقة',
  noUpcoming: 'لا توجد حجوزات قادمة.',
  findCourt: 'ابحث عن ملعب',
  statusConfirmed: 'مؤكد',
  statusCancelled: 'ملغى',
  statusCancelledByVenue: 'ألغته المنشأة',
  statusLateCancellation: 'إلغاء متأخر',
  statusNoShow: 'لم يحضر',
  bookingTitle: 'الحجز',
  cancelBooking: 'إلغاء الحجز',
  cancelTitle: 'إلغاء هذا الحجز؟',
  cancelFree: 'الإلغاء الآن مجاني.',
  cancelLate: 'الإلغاء الآن يُحتسب إلغاءً متأخراً.',
  keepBooking: 'الإبقاء على الحجز',
  bookingCancelled: 'تم إلغاء الحجز.',
  profileTitle: 'حسابي',
  signInForProfile: 'سجّل الدخول لحجز الملاعب وعرض حجوزاتك.',
  nameTitle: 'الاسم',
  addName: 'أضف اسمك',
  phoneTitle: 'الجوال',
  language: 'اللغة',
  english: 'English',
  arabic: 'العربية',
  signOut: 'تسجيل الخروج',
  privacyPolicy: 'سياسة الخصوصية',
  termsConditions: 'الشروط والأحكام',
  lastUpdated: _arLastUpdated,
  openMatches: 'مباريات مفتوحة',
  noMatches: 'لا توجد مباريات مفتوحة لهذه الرياضة في هذا اليوم.',
  levelAny: 'كل المستويات',
  levelName: _arLevel,
  levelLabel: 'المستوى',
  spotsLeft: _arSpotsLeft,
  perPlayer: 'للاعب الواحد',
  matchTitle: 'مباراة مفتوحة',
  playersHeading: 'اللاعبون',
  organiser: 'المنظِّم',
  you: 'أنت',
  sharePayAtDesk: _arShareAtDesk,
  freeLeaveUntil: _arFreeLeave,
  joinMatch: 'انضم إلى المباراة',
  joinAnyway: 'انضم على أي حال',
  matchFull: 'اكتملت هذه المباراة',
  levelMismatch: _arMismatch,
  joined: 'انضممت إلى المباراة.',
  leaveMatch: 'مغادرة المباراة',
  leaveTitle: 'مغادرة هذه المباراة؟',
  leaveFree: 'المغادرة الآن مجانية.',
  leaveLate: 'المغادرة الآن تُحتسب إلغاءً متأخراً.',
  stayIn: 'البقاء',
  leftMatch: 'غادرت المباراة.',
  yourMatch: 'أنت منظِّم هذه المباراة.',
  viewBooking: 'عرض الحجز',
  openToOthers: 'افتح الحجز للاعبين آخرين',
  openToOthersHelp: 'يمكن للآخرين الانضمام ودفع حصة متساوية في الاستقبال، وتدفع أنت عن الأماكن الشاغرة.',
  yourShare: 'حصتك',
  openMatch: 'افتح المباراة',
  saveChanges: 'حفظ التغييرات',
  closeMatch: 'إغلاق المباراة',
  closeBlocked: 'أزل اللاعبين المنضمّين أولاً.',
  remove: 'إزالة',
  removeTitle: _arRemoveTitle,
  notNow: 'ليس الآن',
  matchOpened: 'أصبحت مباراتك مفتوحة للاعبين آخرين.',
  matchClosed: 'تم إغلاق المباراة.',
  matchPill: 'مباراة',
  statusLeft: 'غادرت',
  statusRemoved: 'أزالك المنظِّم',
  yourLevel: 'مستواك',
  yourLevelHelp: 'اضغط على مستواك مرة أخرى لإلغائه. تُعرض المباريات المفتوحة حسبه أولاً.',
  notSet: 'غير محدد',
  playerRemoved: 'تمت إزالة اللاعب.',
  fewerPlayers: 'لاعبون أقل',
  morePlayers: 'لاعبون أكثر',
  pointsTitle: 'نقاطك',
  pointsCount: _arPoints,
  tierName: _arTier,
  gamesToNext: _arGamesToNext,
  topTier: 'وصلت إلى أعلى مستوى.',
  earnMultiplier: _arEarn,
  pointsHistory: 'سجل النقاط',
  noPointsYet: 'لا توجد نقاط بعد. تكسبها عند تسجيل حضورك في الاستقبال.',
  pointsKind: _arKind,
  usePoints: 'استخدم نقاطك',
  usePointsHelp: _arUseHelp,
  pointsHave: _arHave,
  notEnoughPoints: 'نقاطك لا تكفي للاستخدام في هذا الحجز بعد.',
  fewerPoints: 'نقاط أقل',
  morePoints: 'نقاط أكثر',
  pointsOffYouPay: _arOffPay,
  pointsApplied: 'تم تطبيق النقاط.',
  pointsRemoved: 'تمت إزالة النقاط.',
  withPoints: _arWithPoints,
  tournaments: 'البطولات',
  noTournaments: 'لا توجد بطولات قادمة لهذه الرياضة.',
  tournamentTitle: 'البطولة',
  formatName: _arFormat,
  formatHelp: _arFormatHelp,
  teamSize: _arTeamSize,
  teamsCount: _arTeamsCount,
  onWaitlist: _arOnWaitlist,
  registrationCloses: _arRegCloses,
  registrationClosed: 'التسجيل مغلق',
  perTeam: 'لكل فريق',
  feePerTeam: _arFeePerTeam,
  freeEntry: 'المشاركة مجانية',
  teamsHeading: 'الفرق',
  noTeamsYet: 'لا توجد فرق بعد. كن الأول.',
  registerTeam: 'سجّل فريقاً',
  register: 'تسجيل',
  teamNameLabel: 'اسم الفريق',
  teammateLabel: _arTeammate,
  captain: 'القائد',
  teammatesHelp: 'اكتب أسماء زملائك كما يجب أن تظهر في البطولة.',
  requiredField: 'مطلوب',
  teamRegistered: 'تم تسجيل فريقك.',
  teamWaitlisted: 'اكتمل عدد الفرق، لذا أنت على قائمة الانتظار. سنُعلمك إذا توفّر مكان.',
  yourTeam: _arYourTeam,
  statusRegistered: 'مسجَّل',
  statusWaitlist: 'قائمة الانتظار',
  statusCheckedIn: 'تم تسجيل الحضور',
  statusWithdrawn: 'منسحب',
  withdrawTeam: 'سحب الفريق',
  withdrawTitle: 'هل تريد سحب فريقك؟',
  withdrawBody: 'سيتخلى فريقك عن مكانه. يمكنك التسجيل من جديد ما دام التسجيل مفتوحاً.',
  keepTeam: 'إبقاء فريقي',
  teamWithdrawn: 'تم سحب فريقك.',
  tabInfo: 'المعلومات',
  tabDraw: 'القرعة',
  tabFixtures: 'المباريات',
  knockoutRound: _arKnockoutRound,
  groupRound: _arGroupRound,
  groupName: _arGroupName,
  teamColumn: 'الفريق',
  standingsColumns: ['لعب', 'فوز', 'تعادل', 'خسارة', '+/-', 'نقاط'],
  yourNextMatch: 'مباراتك القادمة',
  bye: 'تأهّل مباشر',
  toBeDecided: 'يُحدَّد لاحقاً',
  champion: 'البطل',
  nextMatchLine: _arNextMatch,
);

String _arPoints(int n) => switch (n) {
      1 => 'نقطة واحدة',
      2 => 'نقطتان',
      >= 3 && <= 10 => '$n نقاط',
      _ => '$n نقطة',
    };
String _arTier(String tier) => switch (tier) {
      'Bronze' => 'برونزي',
      'Silver' => 'فضي',
      'Gold' => 'ذهبي',
      _ => tier,
    };
String _arGames(int n) => switch (n) {
      1 => 'مباراة واحدة',
      2 => 'مباراتان',
      >= 3 && <= 10 => '$n مباريات',
      _ => '$n مباراة',
    };
String _arGamesToNext(int games, String tier) => 'تنقصك ${_arGames(games)} للوصول إلى المستوى ال${_arTier(tier)}';
String _arEarn(String m) => 'تكسب النقاط ${ltr('×$m')}';
String _arKind(String kind) => switch (kind) {
      'Earn' => 'نقاط مكتسبة',
      'Redeem' => 'استُخدمت في حجز',
      'EarnReversal' => 'أُلغي تسجيل الحضور',
      'RedeemReleased' => 'أُعيدت إليك',
      _ => kind,
    };
String _arUseHelp(int points, String off, String percent) =>
    '${_arPoints(points)} = خصم $off، بحد أقصى $percent% من مبلغك.';
String _arHave(int n) => 'لديك ${_arPoints(n)}';
String _arOffPay(String off, String pay) => 'خصم $off · تدفع $pay في الاستقبال';
String _arWithPoints(String off) => '$off بالنقاط';

String _arLastUpdated(String date) => 'آخر تحديث: $date';
String _arLevel(String level) => switch (level) {
      'Beginner' => 'مبتدئ',
      'Intermediate' => 'متوسط',
      'Advanced' => 'متقدم',
      _ => level,
    };
String _arSpotsLeft(int n) => switch (n) {
      1 => 'بقي مكان واحد',
      2 => 'بقي مكانان',
      >= 3 && <= 10 => 'بقيت $n أماكن',
      _ => 'بقي $n مكاناً',
    };
String _arShareAtDesk(String price) => 'حصتك: $price، تُدفع في الاستقبال';
String _arFreeLeave(String when) => 'المغادرة مجانية حتى $when';
String _arMismatch(String level) => 'هذه المباراة مخصّصة ${switch (level) {
      'Beginner' => 'للمبتدئين',
      'Advanced' => 'للمتقدمين',
      _ => 'لذوي المستوى ${_arLevel(level)}',
    }}';
String _arRemoveTitle(String name) => 'إزالة $name من المباراة؟';
String _arFrom(String price) => 'ابتداءً من $price';
String _arSlotOpen(String time) => '$time، متاح';
String _arSlotFull(String time) => '$time، محجوز';
String _arFreeHours(int h) => 'إلغاء مجاني حتى ${_arHours(h)} قبل موعد البدء';
String _arBookFor(String price) => 'احجز · $price';
String _arCodeSentTo(String phone) => 'أرسلنا رمزاً من 6 أرقام إلى ${ltr(phone)}.';
String _arResendIn(int s) => 'إعادة الإرسال بعد $s ${s >= 3 && s <= 10 ? 'ثوانٍ' : 'ثانية'}';
String _arPayAmount(String price) => 'ادفع $price نقداً في المنشأة';
String _arFreeUntil(String when) => 'الإلغاء مجاني حتى $when';

String _enFormat(String format) => format == 'RoundRobin' ? 'Round-robin groups' : 'Knockout';
String _enFormatHelp(String format) => format == 'RoundRobin'
    ? 'Each team plays every team in its group; the table decides the winner.'
    : "Lose a match and you're out; winners go through to the final.";
String _enTeamSize(int n) => n == 1 ? '1 player per team' : '$n players per team';
String _enTeamsCount(int registered, int max) => '$registered/$max teams';
String _enOnWaitlist(int n) => '$n on the waitlist';
String _enRegCloses(String when) => 'Registration closes $when';
String _enFeePerTeam(String price) => '$price per team, paid at the venue';
String _enTeammate(int n) => 'Teammate $n';
String _enYourTeam(String name) => 'Your team: $name';
String _enKnockoutRound(int round, int rounds) => switch (rounds - round) {
      0 => 'Final',
      1 => 'Semi-finals',
      2 => 'Quarter-finals',
      _ => 'Round $round',
    };
String _enGroupRound(int round) => 'Round $round';
String _enGroupName(int n) => 'Group $n';
String _enNextMatch(String when, String? court) => court == null ? 'Next match $when' : 'Next match $when on $court';

String _arFormat(String format) => format == 'RoundRobin' ? 'دوري المجموعات' : 'خروج المغلوب';
String _arFormatHelp(String format) => format == 'RoundRobin'
    ? 'يلعب كل فريق ضد جميع فرق مجموعته، ويحدد جدول النقاط الفائز.'
    : 'من يخسر مباراة يخرج، ويتأهل الفائزون حتى النهائي.';
String _arPlayers(int n) => switch (n) {
      1 => 'لاعب واحد',
      2 => 'لاعبان',
      >= 3 && <= 10 => '$n لاعبين',
      _ => '$n لاعباً',
    };
String _arTeamSize(int n) => '${_arPlayers(n)} في كل فريق';
String _arTeamsCount(int registered, int max) => 'الفرق المسجّلة: ${ltr('$registered/$max')}';
String _arOnWaitlist(int n) => 'على قائمة الانتظار: $n';
String _arRegCloses(String when) => 'يُغلق التسجيل $when';
String _arFeePerTeam(String price) => '$price لكل فريق، تُدفع في المنشأة';
String _arTeammate(int n) => 'الزميل $n';
String _arYourTeam(String name) => 'فريقك: $name';
String _arKnockoutRound(int round, int rounds) => switch (rounds - round) {
      0 => 'النهائي',
      1 => 'نصف النهائي',
      2 => 'ربع النهائي',
      _ => 'الدور $round',
    };
String _arGroupRound(int round) => 'الجولة $round';
String _arGroupName(int n) => 'المجموعة $n';
String _arNextMatch(String when, String? court) =>
    court == null ? 'مباراتك القادمة $when' : 'مباراتك القادمة $when في $court';

/// Puts the active language's strings above the navigator.
class StringsScope extends InheritedWidget {
  const StringsScope({super.key, required this.strings, required super.child});
  final Strings strings;

  @override
  bool updateShouldNotify(StringsScope oldWidget) => strings != oldWidget.strings;
}
