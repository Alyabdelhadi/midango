import 'dart:async';

import 'package:flutter/material.dart';

import '../app_config.dart';
import '../l10n/strings.dart';
import '../theme/midango_colors.dart';
import '../widgets/media_image.dart';

/// The operator's home banners: one slide at a time, advancing every 5 s unless the customer is dragging.
class BannerSlider extends StatefulWidget {
  const BannerSlider({super.key, required this.banners, required this.onOpen});

  final List<AppBanner> banners;
  final ValueChanged<AppBanner> onOpen;

  @override
  State<BannerSlider> createState() => _BannerSliderState();
}

class _BannerSliderState extends State<BannerSlider> {
  final _controller = PageController();
  Timer? _timer;
  int _page = 0;

  @override
  void initState() {
    super.initState();
    _start();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _start() {
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 5), (_) {
      final count = widget.banners.length;
      if (count < 2 || !_controller.hasClients) return;
      _controller.animateToPage((_page + 1) % count,
          duration: const Duration(milliseconds: 400), curve: Curves.easeInOut);
    });
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final banners = widget.banners;
    final page = _page < banners.length ? _page : 0;
    return Column(
      children: [
        SizedBox(
          height: 160,
          child: NotificationListener<ScrollNotification>(
            onNotification: (n) {
              if (n is ScrollStartNotification && n.dragDetails != null) _timer?.cancel();
              if (n is ScrollEndNotification && (_timer == null || !_timer!.isActive)) _start();
              return false;
            },
            child: PageView.builder(
              controller: _controller,
              itemCount: banners.length,
              onPageChanged: (i) => setState(() => _page = i),
              itemBuilder: (context, i) {
                final banner = banners[i];
                final image = banner.imageUrl;
                final body = banner.body(s.isArabic);
                return Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  child: Card(
                    margin: EdgeInsets.zero,
                    clipBehavior: Clip.antiAlias,
                    color: MidangoColors.navy,
                    child: InkWell(
                      onTap: banner.linkType == 'None' ? null : () => widget.onOpen(banner),
                      child: Stack(
                        fit: StackFit.expand,
                        children: [
                          if (image != null) ...[
                            MediaImage(image),
                            const DecoratedBox(
                              decoration: BoxDecoration(
                                gradient: LinearGradient(
                                  begin: Alignment.topCenter,
                                  end: Alignment.bottomCenter,
                                  colors: [Color(0x00071329), Color(0xCC071329)],
                                ),
                              ),
                            ),
                          ],
                          Padding(
                            padding: const EdgeInsets.all(16),
                            child: Column(
                              mainAxisAlignment: MainAxisAlignment.end,
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(banner.title(s.isArabic),
                                    maxLines: 2,
                                    overflow: TextOverflow.ellipsis,
                                    style: text.titleMedium?.copyWith(color: MidangoColors.white)),
                                if (body.isNotEmpty) ...[
                                  const SizedBox(height: 4),
                                  Text(body,
                                      maxLines: 2,
                                      overflow: TextOverflow.ellipsis,
                                      style: text.bodySmall?.copyWith(color: MidangoColors.white)),
                                ],
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                );
              },
            ),
          ),
        ),
        if (banners.length > 1) ...[
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              for (var i = 0; i < banners.length; i++)
                AnimatedContainer(
                  duration: const Duration(milliseconds: 200),
                  margin: const EdgeInsets.symmetric(horizontal: 3),
                  width: i == page ? 16 : 6,
                  height: 6,
                  decoration: BoxDecoration(
                    color: i == page ? MidangoColors.navy : MidangoColors.line,
                    borderRadius: BorderRadius.circular(3),
                  ),
                ),
            ],
          ),
        ],
      ],
    );
  }
}

/// "Featured": the operator's picked venues, side by side.
class FeaturedStrip extends StatelessWidget {
  const FeaturedStrip({super.key, required this.venues, required this.onOpen});

  final List<FeaturedVenue> venues;
  final ValueChanged<FeaturedVenue> onOpen;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
          child: Text(s.featuredHeading, style: text.titleMedium),
        ),
        SizedBox(
          height: 178,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            itemCount: venues.length,
            separatorBuilder: (_, _) => const SizedBox(width: 12),
            itemBuilder: (context, i) {
              final venue = venues[i];
              final image = venue.photoUrl ?? venue.logoUrl;
              final address = venue.address(s.isArabic);
              return SizedBox(
                width: 200,
                child: Card(
                  margin: EdgeInsets.zero,
                  clipBehavior: Clip.antiAlias,
                  child: InkWell(
                    onTap: () => onOpen(venue),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        SizedBox(
                          height: 104,
                          child: image == null
                              ? const ColoredBox(color: MidangoColors.disabledBg)
                              : MediaImage(image),
                        ),
                        Padding(
                          padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(venue.name(s.isArabic),
                                  maxLines: 1, overflow: TextOverflow.ellipsis, style: text.titleSmall),
                              if (address.isNotEmpty)
                                Text(address, maxLines: 1, overflow: TextOverflow.ellipsis, style: text.bodySmall),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}
