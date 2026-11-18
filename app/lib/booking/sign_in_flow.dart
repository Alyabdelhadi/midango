import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/api_client.dart';
import '../l10n/strings.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';
import '../widgets/busy_button.dart';

/// Phone → code → name (only when the account has none). True when the customer is signed in with a name.
Future<bool> ensureSignedIn(BuildContext context) async {
  final session = SessionScope.read(context);
  final nav = Navigator.of(context, rootNavigator: true);
  if (!session.signedIn) {
    if (await nav.push<bool>(MaterialPageRoute(builder: (_) => const PhonePage())) != true) return false;
  } else if (session.me == null) {
    try {
      await session.loadMe(); // restored offline earlier; try again now
    } catch (_) {}
  }
  if (!session.hasName) {
    if (await nav.push<bool>(MaterialPageRoute(builder: (_) => const NamePage())) != true) return false;
  }
  return true;
}

/// Turns a failure into the sentence to show: the API's own words, or the generic line.
String _messageOf(Object error, Strings s) => error is ApiException ? error.title : s.somethingWrong;

class PhonePage extends StatefulWidget {
  const PhonePage({super.key});

  @override
  State<PhonePage> createState() => _PhonePageState();
}

class _PhonePageState extends State<PhonePage> {
  final _phone = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _phone.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final s = Strings.of(context);
    final phone = _phone.text.trim();
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await SessionScope.read(context).requestCode(phone);
      if (!mounted) return;
      setState(() => _busy = false);
      final ok = await Navigator.of(context).push<bool>(MaterialPageRoute(builder: (_) => CodePage(phone: phone)));
      if (ok == true && mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = _messageOf(e, s);
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    return _FormPage(
      title: s.signIn,
      heading: s.phoneHeading,
      help: s.phoneHelp,
      error: _error,
      field: TextField(
        controller: _phone,
        autofocus: true,
        keyboardType: TextInputType.phone,
        textDirection: TextDirection.ltr,
        autofillHints: const [AutofillHints.telephoneNumber],
        decoration: InputDecoration(labelText: s.phoneLabel, hintText: s.phoneHint, hintTextDirection: TextDirection.ltr),
        onChanged: (_) => setState(() {}),
        onSubmitted: (_) => _phone.text.trim().isEmpty || _busy ? null : _send(),
      ),
      button: BusyButton(
        label: s.sendCode,
        busy: _busy,
        onPressed: _phone.text.trim().isEmpty ? null : _send,
      ),
    );
  }
}

class CodePage extends StatefulWidget {
  const CodePage({super.key, required this.phone});
  final String phone;

  @override
  State<CodePage> createState() => _CodePageState();
}

class _CodePageState extends State<CodePage> {
  static const _resendAfter = 60;
  final _code = TextEditingController();
  bool _busy = false;
  String? _error;
  int _wait = _resendAfter;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _startCountdown();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _code.dispose();
    super.dispose();
  }

  void _startCountdown() {
    _timer?.cancel();
    _wait = _resendAfter;
    _timer = Timer.periodic(const Duration(seconds: 1), (t) {
      setState(() => _wait--);
      if (_wait <= 0) t.cancel();
    });
  }

  Future<void> _verify() async {
    final s = Strings.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await SessionScope.read(context).verify(widget.phone, _code.text);
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = _messageOf(e, s);
        });
      }
    }
  }

  Future<void> _resend() async {
    final s = Strings.of(context);
    setState(() {
      _error = null;
      _startCountdown();
    });
    try {
      await SessionScope.read(context).requestCode(widget.phone);
    } catch (e) {
      if (mounted) setState(() => _error = _messageOf(e, s));
    }
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final complete = _code.text.length == 6;
    return _FormPage(
      title: s.signIn,
      heading: s.codeHeading,
      help: s.codeSentTo(widget.phone),
      error: _error,
      field: TextField(
        controller: _code,
        autofocus: true,
        keyboardType: TextInputType.number,
        textDirection: TextDirection.ltr,
        maxLength: 6,
        autofillHints: const [AutofillHints.oneTimeCode],
        inputFormatters: [FilteringTextInputFormatter.digitsOnly],
        decoration: InputDecoration(labelText: s.codeLabel, counterText: ''),
        onChanged: (_) => setState(() {}),
        onSubmitted: (_) => complete && !_busy ? _verify() : null,
      ),
      button: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          BusyButton(label: s.verify, busy: _busy, onPressed: complete ? _verify : null),
          const SizedBox(height: 8),
          TextButton(
            onPressed: _wait > 0 ? null : _resend,
            child: Text(_wait > 0 ? s.resendIn(_wait) : s.resendCode),
          ),
        ],
      ),
    );
  }
}

/// "What should the venue call you?" — after the first sign-in, and from the Profile tab to change the name.
class NamePage extends StatefulWidget {
  const NamePage({super.key});

  @override
  State<NamePage> createState() => _NamePageState();
}

class _NamePageState extends State<NamePage> {
  late final _name = TextEditingController(text: SessionScope.read(context).me?.name ?? '');
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final s = Strings.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await SessionScope.read(context).saveName(_name.text.trim());
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = _messageOf(e, s);
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final filled = _name.text.trim().isNotEmpty;
    return _FormPage(
      title: s.nameTitle,
      heading: s.nameHeading,
      help: s.nameHelp,
      error: _error,
      field: TextField(
        controller: _name,
        autofocus: true,
        textCapitalization: TextCapitalization.words,
        autofillHints: const [AutofillHints.name],
        decoration: InputDecoration(labelText: s.nameLabel),
        onChanged: (_) => setState(() {}),
        onSubmitted: (_) => filled && !_busy ? _save() : null,
      ),
      button: BusyButton(label: s.save, busy: _busy, onPressed: filled ? _save : null),
    );
  }
}

/// Heading, one line of help, the field, the API's error if any, and the button.
class _FormPage extends StatelessWidget {
  const _FormPage({
    required this.title,
    required this.heading,
    required this.help,
    required this.field,
    required this.button,
    this.error,
  });

  final String title;
  final String heading;
  final String help;
  final Widget field;
  final Widget button;
  final String? error;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Scaffold(
      backgroundColor: MidangoColors.white,
      appBar: AppBar(title: Text(title)),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            Text(heading, style: text.titleLarge),
            const SizedBox(height: 8),
            Text(help, style: text.bodyLarge),
            const SizedBox(height: 24),
            field,
            if (error != null) ...[
              const SizedBox(height: 12),
              Text(error!, style: text.bodyLarge?.copyWith(color: MidangoColors.error)),
            ],
            const SizedBox(height: 24),
            button,
          ],
        ),
      ),
    );
  }
}
