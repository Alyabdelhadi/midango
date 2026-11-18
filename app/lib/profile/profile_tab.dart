import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../booking/sign_in_flow.dart';
import '../l10n/strings.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';

/// Name, phone, language and sign-out; sign-in and language when signed out.
class ProfileTab extends StatelessWidget {
  const ProfileTab({super.key, required this.onLanguage});

  /// 'en' or 'ar'.
  final ValueChanged<String> onLanguage;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final session = SessionScope.of(context);
    final me = session.me;

    return SafeArea(
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          Text(s.profileTitle, style: text.titleLarge),
          const SizedBox(height: 16),
          if (!session.signedIn) ...[
            Text(s.signInForProfile, style: text.bodyLarge),
            const SizedBox(height: 16),
            FilledButton(onPressed: () => ensureSignedIn(context), child: Text(s.signIn)),
          ] else
            Card(
              margin: EdgeInsets.zero,
              child: Column(
                children: [
                  ListTile(
                    title: Text(s.nameTitle, style: text.bodySmall),
                    subtitle: Text(me?.name ?? s.addName, style: text.bodyLarge),
                    trailing: const Icon(LucideIcons.pencil, size: 20, color: MidangoColors.slate),
                    onTap: () => Navigator.of(context, rootNavigator: true)
                        .push(MaterialPageRoute<bool>(builder: (_) => const NamePage())),
                  ),
                  const Divider(height: 1, color: MidangoColors.line),
                  ListTile(
                    title: Text(s.phoneTitle, style: text.bodySmall),
                    subtitle: Text(ltr(me?.phone ?? ''), style: text.bodyLarge),
                  ),
                ],
              ),
            ),
          const SizedBox(height: 24),
          Text(s.language, style: text.titleMedium),
          const SizedBox(height: 8),
          SegmentedButton<String>(
            segments: [
              ButtonSegment(value: 'en', label: Text(s.english)),
              ButtonSegment(value: 'ar', label: Text(s.arabic)),
            ],
            selected: {s.isArabic ? 'ar' : 'en'},
            showSelectedIcon: false,
            onSelectionChanged: (choice) => onLanguage(choice.first),
            style: SegmentedButton.styleFrom(
              selectedBackgroundColor: MidangoColors.lime,
              selectedForegroundColor: MidangoColors.navy,
              foregroundColor: MidangoColors.navy,
              side: const BorderSide(color: MidangoColors.line),
            ),
          ),
          if (session.signedIn) ...[
            const SizedBox(height: 32),
            OutlinedButton.icon(
              onPressed: session.signOut,
              icon: const Icon(LucideIcons.logOut, size: 20),
              label: Text(s.signOut),
            ),
          ],
        ],
      ),
    );
  }
}
