import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../shared/shared.dart';
import '../../shared/tab_scaffold.dart';
import 'gallery.dart';
import 'gallery_widgets.dart';

/// M-G4: public gallery. Category chips on top, two-column grid of framed artworks, infinite scroll.
class GalleryScreen extends ConsumerStatefulWidget {
  const GalleryScreen({super.key});

  @override
  ConsumerState<GalleryScreen> createState() => _GalleryScreenState();
}

class _GalleryScreenState extends ConsumerState<GalleryScreen> {
  final _scroll = ScrollController();
  String? _category;

  @override
  void initState() {
    super.initState();
    _scroll.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (_scroll.hasClients && _scroll.position.extentAfter < 600) {
      ref.read(galleryListProvider(_category).notifier).loadMore();
    }
  }

  void _select(String? category) {
    if (category == _category) return;
    setState(() => _category = category);
    if (_scroll.hasClients) _scroll.jumpTo(0);
  }

  @override
  Widget build(BuildContext context) {
    final categories = ref.watch(galleryCategoriesProvider).value ?? const <GalleryCategory>[];
    final list = ref.watch(galleryListProvider(_category));

    return TabScaffold(
      title: 'Gallery',
      body: Column(
        children: [
          _CategoryChips(categories: categories, selected: _category, onSelected: _select),
          Expanded(
            child: switch (list) {
              AsyncData(:final value) => _grid(value),
              AsyncError(:final error) => ErrorView(
                  message: ApiException.from(error).message,
                  onRetry: () => ref.invalidate(galleryListProvider(_category)),
                ),
              _ => const Center(child: CircularProgressIndicator()),
            },
          ),
        ],
      ),
    );
  }

  Widget _grid(GalleryListState state) {
    if (state.items.isEmpty) {
      return RefreshIndicator(
        onRefresh: () => ref.refresh(galleryListProvider(_category).future),
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: [
            SizedBox(
              height: 360,
              child: EmptyState(
                title: 'No artworks here yet',
                message: _category == null ? 'Check back soon.' : 'Try another category.',
                actionLabel: _category == null ? null : 'Show all',
                onAction: _category == null ? null : () => _select(null),
              ),
            ),
          ],
        ),
      );
    }

    final tiles = [
      for (final a in state.items)
        galleryTile(a, hero: true, onTap: () => context.push('/gallery/artworks/${a.submissionId}')),
    ];
    final rows = twoColumnRowCount(tiles.length);
    return RefreshIndicator(
      onRefresh: () => ref.refresh(galleryListProvider(_category).future),
      child: CustomScrollView(
        controller: _scroll,
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: [
          const SliverToBoxAdapter(child: SizedBox(height: AppSpacing.md)),
          SliverList.builder(itemCount: rows, itemBuilder: (context, i) => twoColumnRow(tiles, i)),
          SliverToBoxAdapter(child: _footer(state)),
        ],
      ),
    );
  }

  Widget _footer(GalleryListState state) {
    if (state.loadingMore) {
      return const Padding(padding: EdgeInsets.all(AppSpacing.lg), child: Center(child: CircularProgressIndicator()));
    }
    if (state.loadMoreError != null) {
      return Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          children: [
            Text(state.loadMoreError!, textAlign: TextAlign.center),
            const SizedBox(height: AppSpacing.sm),
            OutlinedButton(
              onPressed: () => ref.read(galleryListProvider(_category).notifier).loadMore(),
              child: const Text('Try again'),
            ),
          ],
        ),
      );
    }
    return const SizedBox(height: AppSpacing.lg);
  }
}

class _CategoryChips extends StatelessWidget {
  const _CategoryChips({required this.categories, required this.selected, required this.onSelected});

  final List<GalleryCategory> categories;
  final String? selected;
  final ValueChanged<String?> onSelected;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 56,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.gutter, vertical: AppSpacing.sm),
        children: [
          _chip('All', selected == null, () => onSelected(null)),
          for (final c in categories) _chip(c.label, selected == c.apiValue, () => onSelected(c.apiValue)),
        ],
      ),
    );
  }

  Widget _chip(String label, bool isSelected, VoidCallback onTap) => Padding(
        padding: const EdgeInsets.only(right: AppSpacing.sm),
        child: ChoiceChip(label: Text(label), selected: isSelected, onSelected: (_) => onTap()),
      );
}
