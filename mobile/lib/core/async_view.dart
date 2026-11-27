import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../app/motion.dart';
import 'api_client.dart';

/// Whether [value] should be shown as content: it has data, and is at most being refreshed (pull to refresh, or
/// invalidated after a save). The content then stays on screen while the new data loads, instead of flashing a
/// skeleton. A reload because what is asked changed (another filter: `isReloading`) does show the loading state.
bool showsContent(AsyncValue<Object?> value) => value.hasValue && !value.hasError && !value.isReloading;

/// Shows an AsyncValue: loading → a [Skeleton] of [data] built from [placeholder], or a spinner; error → message
/// and "Try again"; data → [data], kept on screen while it is refreshed ([showsContent]). The states hand over with a
/// fade through ([StateSwitcher]).
class AsyncValueView<T> extends StatelessWidget {
  const AsyncValueView({super.key, required this.value, required this.data, required this.onRetry, this.placeholder});

  final AsyncValue<T> value;
  final Widget Function(T data) data;
  final VoidCallback onRetry;

  /// Stand-in data of the same shape as the real data (a few typical items). While loading, [data] is built with it
  /// as a [Skeleton]: grey bars exactly where the real text and pictures will be, so nothing moves when they
  /// arrive. A spinner when null.
  final T? placeholder;

  @override
  Widget build(BuildContext context) {
    final child = switch (value) {
      _ when showsContent(value) => KeyedSubtree(key: const ValueKey('data'), child: data(value.requireValue)),
      AsyncError(:final error) => ErrorView(
        key: const ValueKey('error'),
        message: ApiException.from(error).message,
        onRetry: onRetry,
      ),
      _ => KeyedSubtree(
        key: const ValueKey('loading'),
        child: placeholder != null
            ? Skeleton(child: data(placeholder as T))
            : const Center(child: CircularProgressIndicator()),
      ),
    };
    return StateSwitcher(child: child);
  }
}

/// Shared error block: what went wrong and a way to try again.
class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.cloud_off, size: 48, color: Theme.of(context).colorScheme.error),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            OutlinedButton(onPressed: onRetry, child: const Text('Try again')),
          ],
        ),
      ),
    );
  }
}

/// Placeholder block for features that come later.
class ComingSoon extends StatelessWidget {
  const ComingSoon({super.key, required this.title, required this.note});

  final String title;
  final String note;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.construction, size: 48),
            const SizedBox(height: 12),
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            Text(note, textAlign: TextAlign.center),
          ],
        ),
      ),
    );
  }
}
