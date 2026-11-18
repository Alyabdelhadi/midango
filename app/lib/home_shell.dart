import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import 'l10n/strings.dart';
import 'theme/midango_colors.dart';

/// The selected tab. Pages pushed above the shell (the confirmation) switch tabs through it.
final shellTab = ValueNotifier<int>(0);

/// Bumped whenever a booking is made or cancelled, so lists of times and bookings reload.
final bookingsChanged = ValueNotifier<int>(0);

class HomeShell extends StatelessWidget {
  const HomeShell({super.key, required this.onLanguage});

  /// 'en' or 'ar'; saved by the app.
  final ValueChanged<String> onLanguage;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    return ValueListenableBuilder<int>(
      valueListenable: shellTab,
      builder: (context, index, _) => Scaffold(
        body: IndexedStack(
          index: index,
          children: [
            Center(child: Text(s.playTitle)),
            Center(child: Text(s.bookingsTitle)),
            Center(child: Text(s.profileTitle)),
          ],
        ),
        bottomNavigationBar: NavigationBar(
          selectedIndex: index,
          onDestinationSelected: (i) => shellTab.value = i,
          backgroundColor: MidangoColors.white,
          indicatorColor: MidangoColors.lime,
          destinations: [
            NavigationDestination(icon: const Icon(LucideIcons.calendarPlus), label: s.tabPlay),
            NavigationDestination(icon: const Icon(LucideIcons.ticket), label: s.tabBookings),
            NavigationDestination(icon: const Icon(LucideIcons.user), label: s.tabProfile),
          ],
        ),
      ),
    );
  }
}
