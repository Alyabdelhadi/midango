import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../api/models.dart';
import '../booking/sign_in_flow.dart';
import '../l10n/strings.dart';
import '../session/session.dart';
import '../play/pickers.dart';
import '../theme/midango_colors.dart';
import '../widgets/sport_icon.dart';
import 'legal_page.dart';
import 'points.dart';

/// Name, phone, points, level per sport, language and sign-out; sign-in and language when signed out.
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
          if (session.signedIn) const PointsCard(),
          if (session.signedIn) const _Levels(),
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
          const SizedBox(height: 24),
          Card(
            margin: EdgeInsets.zero,
            child: Column(
              children: [
                for (final (i, (kind, title)) in [('privacy', s.privacyPolicy), ('terms', s.termsConditions)].indexed) ...[
                  if (i > 0) const Divider(height: 1, color: MidangoColors.line),
                  ListTile(
                    title: Text(title, style: text.bodyLarge),
                    trailing: Icon(s.isArabic ? LucideIcons.chevronLeft : LucideIcons.chevronRight,
                        size: 20, color: MidangoColors.slate),
                    onTap: () => Navigator.of(context, rootNavigator: true)
                        .push(MaterialPageRoute<void>(builder: (_) => LegalPage(kind: kind, title: title))),
                  ),
                ],
              ],
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

/// "Your level": Beginner / Intermediate / Advanced for each sport; tapping the chosen one clears it.
class _Levels extends StatefulWidget {
  const _Levels();

  @override
  State<_Levels> createState() => _LevelsState();
}

class _LevelsState extends State<_Levels> {
  List<Category>? _categories;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final session = SessionScope.read(context);
    try {
      final json = await session.api.get('/categories') as List;
      await session.ensureLevels();
      if (mounted) setState(() => _categories = json.map((j) => Category.fromJson(j as JsonMap)).toList());
    } catch (_) {
      // Offline: the section stays hidden until the tab is opened again.
    }
  }

  Future<void> _set(Category sport, String? level) async {
    final s = Strings.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await SessionScope.read(context).setLevel(sport.id, level);
    } catch (_) {
      messenger.showSnackBar(SnackBar(content: Text(s.somethingWrong)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final levels = SessionScope.of(context).levels ?? const {};
    final categories = _categories;
    if (categories == null || categories.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(top: 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(s.yourLevel, style: text.titleMedium),
          const SizedBox(height: 4),
          Text(s.yourLevelHelp, style: text.bodySmall),
          for (final sport in categories) ...[
            const SizedBox(height: 16),
            Row(
              children: [
                SportIcon(iconKey: sport.iconKey, iconUrl: sport.iconUrl, color: MidangoColors.navy),
                const SizedBox(width: 8),
                Expanded(child: Text(sport.name(s.isArabic), style: text.labelLarge)),
                if (levels[sport.id] == null) Text(s.notSet, style: text.bodySmall),
              ],
            ),
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: SegmentedButton<String>(
                segments: [for (final l in matchLevels) ButtonSegment(value: l, label: Text(s.levelName(l)))],
                selected: {?levels[sport.id]},
                emptySelectionAllowed: true,
                showSelectedIcon: false,
                onSelectionChanged: (choice) => _set(sport, choice.firstOrNull),
                style: SegmentedButton.styleFrom(
                  selectedBackgroundColor: MidangoColors.lime,
                  selectedForegroundColor: MidangoColors.navy,
                  foregroundColor: MidangoColors.navy,
                  side: const BorderSide(color: MidangoColors.line),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
