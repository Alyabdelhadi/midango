import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import 'media_image.dart';

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

/// A sport's uploaded icon when it has one, else its built-in Lucide icon.
class SportIcon extends StatelessWidget {
  const SportIcon({super.key, required this.iconKey, this.iconUrl, this.size = 18, this.color});

  final String iconKey;
  final String? iconUrl;
  final double size;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final url = iconUrl;
    if (url == null) return Icon(sportIcon(iconKey), size: size, color: color);
    return ClipRRect(borderRadius: BorderRadius.circular(4), child: MediaImage(url, width: size + 2, height: size + 2));
  }
}
