import 'package:flutter/material.dart';

/// Scaffold with the navy app bar for a tab page. Screens that already render only a body
/// (the old tab widgets) are wrapped in this; screens with a hero or custom bar build their own `Scaffold`.
class TabScaffold extends StatelessWidget {
  const TabScaffold({super.key, required this.title, required this.body, this.actions, this.floatingActionButton});

  final String title;
  final Widget body;
  final List<Widget>? actions;
  final Widget? floatingActionButton;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title), actions: actions),
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
