import 'package:flutter/material.dart';

import '../api/models.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';
import '../widgets/error_retry.dart';

/// The privacy policy or the terms (`kind` = 'privacy' | 'terms'), in the app's language.
class LegalPage extends StatefulWidget {
  const LegalPage({super.key, required this.kind, required this.title});
  final String kind;
  final String title;

  @override
  State<LegalPage> createState() => _LegalPageState();
}

class _LegalPageState extends State<LegalPage> {
  JsonMap? _doc;
  bool _failed = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _load());
  }

  Future<void> _load() async {
    if (!mounted) return;
    final api = SessionScope.read(context).api;
    setState(() => _failed = false);
    try {
      final doc = await api.get('/legal/${widget.kind}') as JsonMap;
      if (mounted) setState(() => _doc = doc);
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final doc = _doc;
    return Scaffold(
      appBar: AppBar(title: Text(widget.title)),
      body: doc == null
          ? Center(child: _failed ? ErrorRetry(onRetry: _load) : const CircularProgressIndicator())
          : SelectionArea(
              child: ListView(
                padding: const EdgeInsets.all(24),
                children: [
                  if (doc['updatedAt'] case final String updated) ...[
                    Text(s.lastUpdated(_date(updated, s)), style: text.bodySmall?.copyWith(color: MidangoColors.slate)),
                    const SizedBox(height: 16),
                  ],
                  ..._render((s.isArabic ? doc['bodyAr'] : doc['bodyEn']) as String? ?? '', text),
                ],
              ),
            ),
    );
  }
}

/// "Wed 18 Nov 2026" — formatDay plus the year, since a legal date needs one.
String _date(String iso, Strings s) => '${formatDay(dayOf(iso), s)} ${ltr(iso.substring(0, 4))}';

/// The legal body subset: `# ` / `## ` headings, blank-line paragraphs, `- ` bullets, `**bold**`.
List<Widget> _render(String body, TextTheme text) {
  final out = <Widget>[];
  final para = <String>[];
  void flush() {
    if (para.isEmpty) return;
    out.add(Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Text.rich(_inline(para.join(' '), text.bodyLarge)),
    ));
    para.clear();
  }

  for (final raw in body.replaceAll('\r', '').split('\n')) {
    final line = raw.trim();
    if (line.isEmpty) {
      flush();
    } else if (line.startsWith('## ') || line.startsWith('# ')) {
      flush();
      final h1 = line.startsWith('# ');
      out.add(Padding(
        padding: EdgeInsets.only(top: out.isEmpty ? 0 : 12, bottom: 8),
        child: Text.rich(_inline(line.substring(h1 ? 2 : 3), h1 ? text.titleLarge : text.titleMedium)),
      ));
    } else if (line.startsWith('- ')) {
      flush();
      out.add(Padding(
        padding: const EdgeInsetsDirectional.only(start: 8, bottom: 6),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('•  ', style: text.bodyLarge),
            Expanded(child: Text.rich(_inline(line.substring(2), text.bodyLarge))),
          ],
        ),
      ));
    } else {
      para.add(line);
    }
  }
  flush();
  return out;
}

/// `**bold**` runs: every odd segment between `**` markers is bold.
TextSpan _inline(String line, TextStyle? style) {
  final parts = line.split('**');
  return TextSpan(style: style, children: [
    for (var i = 0; i < parts.length; i++)
      if (parts[i].isNotEmpty)
        TextSpan(text: parts[i], style: i.isOdd ? const TextStyle(fontWeight: FontWeight.w700) : null),
  ]);
}
