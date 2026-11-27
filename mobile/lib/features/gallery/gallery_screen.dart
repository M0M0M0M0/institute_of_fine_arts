import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
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
          // How many works are on the walls (for the current category); a grey bar in its place while loading,
          // so the grid below does not move down when the count arrives
          if (list.value case final state? when state.items.isNotEmpty)
            _count(state)
          else if (list.isLoading && !list.hasValue)
            Skeleton(child: _count(Placeholders.gallery)),
          Expanded(
            child: StateSwitcher(
              child: switch (list) {
                AsyncValue(:final value?) when showsContent(list) => KeyedSubtree(key: const ValueKey('data'), child: _grid(value)),
                AsyncError(:final error) => ErrorView(
                    key: const ValueKey('error'),
                    message: ApiException.from(error).message,
                    onRetry: () => ref.invalidate(galleryListProvider(_category)),
                  ),
                _ => Skeleton(key: const ValueKey('loading'), child: _grid(Placeholders.gallery, skeleton: true)),
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _count(GalleryListState state) {
    final gutter = AppLayout.gutterFor(MediaQuery.sizeOf(context).width);
    return Padding(
      padding: EdgeInsets.fromLTRB(gutter, AppSpacing.md, gutter, 0),
      child: Align(
        alignment: Alignment.centerLeft,
        child: Text(
          '${state.totalCount} ${state.totalCount == 1 ? 'work' : 'works'}',
          style: Theme.of(context).textTheme.bodySmall,
        ),
      ),
    );
  }

  /// The wall of works; [skeleton] for the loading stand-in (no scroll controller, no hero, nothing to load).
  Widget _grid(GalleryListState state, {bool skeleton = false}) {
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
        galleryTile(a, hero: !skeleton, onTap: () => context.push('/gallery/artworks/${a.submissionId}')),
    ];
    final width = MediaQuery.sizeOf(context).width;
    final columns = AppLayout.columnsFor(width);
    final gutter = AppLayout.gutterFor(width);
    final rows = tileRowCount(tiles.length, columns);
    return RefreshIndicator(
      onRefresh: () => ref.refresh(galleryListProvider(_category).future),
      child: CustomScrollView(
        controller: skeleton ? null : _scroll,
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: [
          const SliverToBoxAdapter(child: SizedBox(height: AppSpacing.lg)),
          SliverList.builder(itemCount: rows, itemBuilder: (context, i) => tileRow(tiles, i, columns, gutter: gutter)),
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
    final gutter = AppLayout.gutterFor(MediaQuery.sizeOf(context).width);
    final children = <Widget>[_Filter('All', selected == null, () => onSelected(null))];
    for (var i = 0; i < categories.length; i++) {
      final c = categories[i];
      // A thin rule marks the start of a new group (subject, medium, style …)
      if (i == 0 || categories[i - 1].group != c.group) children.add(const _GroupRule());
      children.add(_Filter(c.label, selected == c.apiValue, () => onSelected(c.apiValue)));
    }
    // The hairline runs the full width; the filters scroll sideways above it
    return Padding(
      padding: EdgeInsets.symmetric(horizontal: gutter),
      child: DecoratedBox(
        decoration: const BoxDecoration(border: Border(bottom: BorderSide(color: AppColors.line))),
        child: SizedBox(
          height: AppSpacing.touch,
          child: ScrollEdgeFade(child: ListView(scrollDirection: Axis.horizontal, children: children)),
        ),
      ),
    );
  }
}

/// One category as a small-caps word; the current one is ink with a brass rule under it.
class _Filter extends StatelessWidget {
  const _Filter(this.label, this.active, this.onTap);

  final String label;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      selected: active,
      child: InkWell(
        onTap: () {
          if (!active) HapticFeedback.selectionClick();
          onTap();
        },
        child: Container(
          alignment: Alignment.center,
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md),
          decoration: BoxDecoration(
            border: Border(bottom: BorderSide(color: active ? AppColors.brass : Colors.transparent, width: 2)),
          ),
          child: Text(
            label.toUpperCase(),
            style: Theme.of(context).textTheme.labelSmall!.copyWith(
                  fontSize: 12.5,
                  letterSpacing: 1.6,
                  color: active ? AppColors.ink : AppColors.muted,
                ),
          ),
        ),
      ),
    );
  }
}

class _GroupRule extends StatelessWidget {
  const _GroupRule();

  @override
  Widget build(BuildContext context) => const Padding(
        padding: EdgeInsets.symmetric(horizontal: AppSpacing.xs, vertical: 14),
        child: VerticalDivider(width: 1, color: AppColors.line),
      );
}
