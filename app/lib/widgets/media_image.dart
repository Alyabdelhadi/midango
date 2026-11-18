import 'package:flutter/widgets.dart';

import '../config.dart';
import '../theme/midango_colors.dart';

/// An uploaded image ("/media/…" from the API). Decorative: a flat placeholder while loading or on error,
/// never a broken-image icon.
class MediaImage extends StatelessWidget {
  const MediaImage(this.url, {super.key, this.width, this.height});

  final String url;
  final double? width;
  final double? height;

  @override
  Widget build(BuildContext context) {
    final placeholder = SizedBox(width: width, height: height, child: const ColoredBox(color: MidangoColors.disabledBg));
    return Image.network(
      '$apiBase$url',
      width: width,
      height: height,
      fit: BoxFit.cover,
      excludeFromSemantics: true,
      frameBuilder: (_, child, frame, _) => frame == null ? placeholder : child,
      errorBuilder: (_, _, _) => placeholder,
    );
  }
}
