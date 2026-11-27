import 'package:flutter/material.dart';

import '../app/tokens.dart';
import '../features/notifications/notifications_screen.dart';

/// Scaffold with the navy app bar for a tab page. Screens that already render only a body
/// (the old tab widgets) are wrapped in this; screens with a hero or custom bar build their own `Scaffold`.
class TabScaffold extends StatelessWidget {
  const TabScaffold({
    super.key,
    required this.title,
    required this.body,
    this.actions,
    this.floatingActionButton,
    this.showHeading = true,
  });

  final String title;
  final Widget body;
  final List<Widget>? actions;
  final Widget? floatingActionButton;

  /// On wide screens the title is shown as a large heading above the body. A page that has its own big heading
  /// (like the greeting on the home page) turns it off.
  final bool showHeading;

  @override
  Widget build(BuildContext context) {
    final width = MediaQuery.sizeOf(context).width;
    // Wide screens already have the top navigation: show the title as a large page heading instead of a bar
    if (width >= AppLayout.wide) {
      if (!showHeading) return Scaffold(body: body, floatingActionButton: floatingActionButton);
      final text = Theme.of(context).textTheme;
      final gutter = AppLayout.gutterFor(width);
      return Scaffold(
        floatingActionButton: floatingActionButton,
        body: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: EdgeInsets.fromLTRB(gutter, AppSpacing.xl, gutter, AppSpacing.md),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const SizedBox(width: 28, height: 2, child: ColoredBox(color: AppColors.brass)),
                        const SizedBox(height: AppSpacing.md),
                        Semantics(header: true, child: Text(title, style: text.displaySmall)),
                      ],
                    ),
                  ),
                  ...?actions,
                  const NotificationBell(),
                ],
              ),
            ),
            Expanded(child: body),
          ],
        ),
      );
    }
    // A page with its own big greeting needs no bar on a phone: the bar would only repeat "Home" above it
    if (!showHeading) {
      // The bell sits in the top corner, above the page's own greeting
      return Scaffold(
        body: SafeArea(
          bottom: false,
          child: Stack(
            children: [
              body,
              const Positioned(top: AppSpacing.xs, right: AppSpacing.xs, child: NotificationBell()),
            ],
          ),
        ),
        floatingActionButton: floatingActionButton,
      );
    }
    return Scaffold(
      appBar: AppBar(title: Text(title), actions: [...?actions, const NotificationBell()]),
      body: body,
      floatingActionButton: floatingActionButton,
    );
  }
}

/// Body of a placeholder tab that a later phase replaces.
class PlaceholderBody extends StatelessWidget {
  const PlaceholderBody({super.key, required this.title, this.note = 'This screen is coming soon.'});

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
            const Icon(Icons.construction_outlined, size: 44),
            const SizedBox(height: 12),
            Text(title, style: Theme.of(context).textTheme.titleMedium, textAlign: TextAlign.center),
            const SizedBox(height: 4),
            Text(note, textAlign: TextAlign.center),
          ],
        ),
      ),
    );
  }
}
