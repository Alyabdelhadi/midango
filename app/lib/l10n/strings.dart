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
);

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
);

String _arFrom(String price) => 'ابتداءً من $price';
String _arSlotOpen(String time) => '$time، متاح';
String _arSlotFull(String time) => '$time، محجوز';
String _arFreeHours(int h) => 'إلغاء مجاني حتى ${_arHours(h)} قبل موعد البدء';
String _arBookFor(String price) => 'احجز · $price';
String _arCodeSentTo(String phone) => 'أرسلنا رمزاً من 6 أرقام إلى ${ltr(phone)}.';
String _arResendIn(int s) => 'إعادة الإرسال بعد $s ${s >= 3 && s <= 10 ? 'ثوانٍ' : 'ثانية'}';
String _arPayAmount(String price) => 'ادفع $price نقداً في المنشأة';
String _arFreeUntil(String when) => 'الإلغاء مجاني حتى $when';

/// Puts the active language's strings above the navigator.
class StringsScope extends InheritedWidget {
  const StringsScope({super.key, required this.strings, required super.child});
  final Strings strings;

  @override
  bool updateShouldNotify(StringsScope oldWidget) => strings != oldWidget.strings;
}
