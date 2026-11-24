import 'package:flutter/material.dart';

import 'tokens.dart';

/// On wide screens (web, tablet) the app is a centred page of [AppLayout.maxWidth] standing on a stone floor,
/// like a catalogue page, instead of a phone layout stretched across the window.
/// Below that width it does nothing.
///
/// The child sees the page width through `MediaQuery`, so breakpoints inside the app follow the page, not the window.
class WideCanvas extends StatelessWidget {
  const WideCanvas({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    if (media.size.width <= AppLayout.maxWidth) return child;

    final page = Size(AppLayout.maxWidth, media.size.height);
    return ColoredBox(
      color: AppColors.floor,
      child: Center(
        child: SizedBox(
          width: page.width,
          child: DecoratedBox(
            decoration: const BoxDecoration(
              color: AppColors.wall,
              border: Border.symmetric(vertical: BorderSide(color: AppColors.line)),
            ),
            child: MediaQuery(data: media.copyWith(size: page), child: child),
          ),
        ),
      ),
    );
  }
}
