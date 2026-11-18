import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

/// The same keys the admin's icon picker offers (admin/src/lib/sportIcons.tsx).
const _icons = <String, IconData>{
  'volleyball': LucideIcons.volleyball,
  'goal': LucideIcons.goal,
  'trophy': LucideIcons.trophy,
  'dumbbell': LucideIcons.dumbbell,
  'target': LucideIcons.target,
  'circle-dot': LucideIcons.circleDot,
  'bike': LucideIcons.bike,
  'waves': LucideIcons.waves,
  'mountain': LucideIcons.mountain,
  'footprints': LucideIcons.footprints,
  'medal': LucideIcons.medal,
  'award': LucideIcons.award,
  'flag': LucideIcons.flag,
  'timer': LucideIcons.timer,
  'activity': LucideIcons.activity,
  'users': LucideIcons.users,
};

IconData sportIcon(String iconKey) => _icons[iconKey] ?? LucideIcons.trophy;
