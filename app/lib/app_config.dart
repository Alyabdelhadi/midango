import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:url_launcher/url_launcher.dart';

import 'api/models.dart';
import 'l10n/strings.dart';
import 'session/session.dart';
import 'theme/midango_colors.dart';

String _pick(bool arabic, String? ar, String? en) => arabic && (ar ?? '').isNotEmpty ? ar! : en ?? '';

/// What the operator controls from Business (GET /app/config): banners, featured venues, force update, maintenance.
class AppConfig {
  AppConfig.fromJson(JsonMap j)
      : minAndroidBuild = j['minAndroidBuild'] as int? ?? 1,
        minIosBuild = j['minIosBuild'] as int? ?? 1,
        androidStoreUrl = j['androidStoreUrl'] as String?,
        iosStoreUrl = j['iosStoreUrl'] as String?,
        maintenance = j['maintenance'] as bool? ?? false,
        maintenanceMessageEn = j['maintenanceMessageEn'] as String?,
        maintenanceMessageAr = j['maintenanceMessageAr'] as String?,
        banners = (j['banners'] as List? ?? const []).map((e) => AppBanner.fromJson(e as JsonMap)).toList(),
        featured = (j['featured'] as List? ?? const []).map((e) => FeaturedVenue.fromJson(e as JsonMap)).toList();

  final int minAndroidBuild;
  final int minIosBuild;
  final String? androidStoreUrl;
  final String? iosStoreUrl;
  final bool maintenance;
  final String? maintenanceMessageEn;
  final String? maintenanceMessageAr;
  final List<AppBanner> banners;
  final List<FeaturedVenue> featured;

  String maintenanceMessage(bool arabic) => _pick(arabic, maintenanceMessageAr, maintenanceMessageEn);
}

class AppBanner {
  AppBanner.fromJson(JsonMap j)
      : id = j['id'] as String,
        titleEn = j['titleEn'] as String,
        titleAr = j['titleAr'] as String?,
        bodyEn = j['bodyEn'] as String?,
        bodyAr = j['bodyAr'] as String?,
        imageUrl = j['imageUrl'] as String?,
        linkType = j['linkType'] as String? ?? 'None',
        linkTarget = j['linkTarget'] as String?;

  final String id;
  final String titleEn;
  final String? titleAr;
  final String? bodyEn;
  final String? bodyAr;
  final String? imageUrl;

  /// "None" | "Venue" | "Tournament" | "Url".
  final String linkType;
  final String? linkTarget;

  String title(bool arabic) => _pick(arabic, titleAr, titleEn);
  String body(bool arabic) => _pick(arabic, bodyAr, bodyEn);
}

class FeaturedVenue {
  FeaturedVenue.fromJson(JsonMap j)
      : id = j['id'] as String,
        nameEn = j['nameEn'] as String,
        nameAr = j['nameAr'] as String?,
        logoUrl = j['logoUrl'] as String?,
        photoUrl = j['photoUrl'] as String?,
        addressEn = j['addressEn'] as String?,
        addressAr = j['addressAr'] as String?,
        categoryIds = (j['categoryIds'] as List? ?? const []).cast<String>();

  final String id;
  final String nameEn;
  final String? nameAr;
  final String? logoUrl;
  final String? photoUrl;
  final String? addressEn;
  final String? addressAr;
  final List<String> categoryIds;

  String name(bool arabic) => _pick(arabic, nameAr, nameEn);
  String address(bool arabic) => _pick(arabic, addressAr, addressEn);
}

/// The last config fetched; null until one arrives (or if none ever does: then nothing is gated or shown).
final appConfig = ValueNotifier<AppConfig?>(null);

/// Opens an https link outside the app (store, banner). Failures are silent.
Future<void> openExternal(String url) async {
  final uri = Uri.tryParse(url);
  if (uri == null) return;
  try {
    await launchUrl(uri, mode: LaunchMode.externalApplication);
  } catch (_) {}
}

/// Above [HomeShell]: fetches the config at start and on resume, and blocks the app for maintenance or a
/// too-old build. A failed fetch keeps the last config (none at start), so the app opens normally.
class AppGate extends StatefulWidget {
  const AppGate({super.key, required this.child});

  final Widget child;

  @override
  State<AppGate> createState() => _AppGateState();
}

class _AppGateState extends State<AppGate> {
  late final AppLifecycleListener _lifecycle;
  int? _build;

  @override
  void initState() {
    super.initState();
    _lifecycle = AppLifecycleListener(onResume: _load);
    _load();
    if (!kIsWeb) {
      PackageInfo.fromPlatform().then((info) {
        if (mounted) setState(() => _build = int.tryParse(info.buildNumber));
      }).catchError((_) {});
    }
  }

  @override
  void dispose() {
    _lifecycle.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      appConfig.value = AppConfig.fromJson(await SessionScope.read(context).api.get('/app/config') as JsonMap);
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) => ValueListenableBuilder<AppConfig?>(
        valueListenable: appConfig,
        builder: (context, config, child) {
          if (config == null) return child!;
          final s = Strings.of(context);
          if (config.maintenance) {
            final message = config.maintenanceMessage(s.isArabic);
            return _BlockingPage(
              title: s.maintenanceTitle,
              body: message,
              action: s.tryAgain,
              onAction: _load,
            );
          }
          final build = _build;
          final (min, store) = switch (defaultTargetPlatform) {
            TargetPlatform.android => (config.minAndroidBuild, config.androidStoreUrl),
            TargetPlatform.iOS => (config.minIosBuild, config.iosStoreUrl),
            _ => (0, null),
          };
          if (!kIsWeb && build != null && build < min) {
            return _BlockingPage(
              title: s.updateTitle,
              body: s.updateBody,
              action: store == null || store.isEmpty ? null : s.updateButton,
              onAction: () => openExternal(store!),
            );
          }
          return child!;
        },
        child: widget.child,
      );
}

/// Full screen: the logo, a title, a sentence and at most one button.
class _BlockingPage extends StatelessWidget {
  const _BlockingPage({required this.title, required this.body, required this.action, required this.onAction});

  final String title;
  final String body;
  final String? action;
  final VoidCallback onAction;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Scaffold(
      backgroundColor: MidangoColors.white,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(32),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Image.asset('assets/brand/midango-icon.png', width: 88, height: 88, semanticLabel: 'Midango'),
                const SizedBox(height: 24),
                Text(title, style: text.titleLarge, textAlign: TextAlign.center),
                if (body.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Text(body, style: text.bodyLarge, textAlign: TextAlign.center),
                ],
                if (action != null) ...[
                  const SizedBox(height: 24),
                  FilledButton(onPressed: onAction, child: Text(action!)),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
