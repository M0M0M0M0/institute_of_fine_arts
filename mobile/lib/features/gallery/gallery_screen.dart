import 'package:flutter/material.dart';

import '../../shared/tab_scaffold.dart';

/// PLACEHOLDER (M-G4) – replaced by the guest phase-2 agent.
/// Data is ready: `galleryListProvider(category)` and `galleryCategoriesProvider` in gallery.dart.
class GalleryScreen extends StatelessWidget {
  const GalleryScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const TabScaffold(title: 'Gallery', body: PlaceholderBody(title: 'Gallery'));
  }
}
