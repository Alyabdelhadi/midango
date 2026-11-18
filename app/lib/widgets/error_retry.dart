import 'package:flutter/material.dart';

import '../l10n/strings.dart';

/// A failed load: the message and a "Try again" button.
class ErrorRetry extends StatelessWidget {
  const ErrorRetry({super.key, this.message, required this.onRetry});

  /// Defaults to the generic "Something went wrong" line.
  final String? message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(message ?? s.somethingWrong, textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyLarge),
          const SizedBox(height: 12),
          OutlinedButton(onPressed: onRetry, child: Text(s.tryAgain)),
        ],
      ),
    );
  }
}
