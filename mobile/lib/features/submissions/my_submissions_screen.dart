import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../shared/shared.dart';
import '../../shared/tab_scaffold.dart';
import '../competitions/competitions.dart';
import '../student/dashboard.dart';
import 'results_tab.dart';
import 'submissions.dart';
import 'submission_widgets.dart';

/// M-S4 "My entries": filters, entries with thumbnails and their state (Submitted / Waiting for results /
/// rating / award). The old "Results & awards" tab lives here too: awards of the competitions the student entered.
class MySubmissionsScreen extends ConsumerStatefulWidget {
  const MySubmissionsScreen({super.key});

  @override
  ConsumerState<MySubmissionsScreen> createState() => _MySubmissionsScreenState();
}

class _MySubmissionsScreenState extends ConsumerState<MySubmissionsScreen> {
  late final TextEditingController _search;

  @override
  void initState() {
    super.initState();
    _search = TextEditingController(text: ref.read(submissionFilterProvider).q);
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _refresh() async {
    ref.invalidate(allMySubmissionsProvider);
    ref.invalidate(competitionAwardsProvider);
    ref.invalidate(studentDashboardProvider);
    ref.invalidate(mySubmissionsProvider);
    await ref.read(mySubmissionsProvider.future);
  }

  @override
  Widget build(BuildContext context) {
    final filter = ref.watch(submissionFilterProvider);
    final notifier = ref.read(submissionFilterProvider.notifier);
    final competitions = ref.watch(competitionListProvider).value ?? const <CompetitionSummary>[];
    final list = ref.watch(mySubmissionsProvider);
    final width = MediaQuery.sizeOf(context).width;
    final wide = width >= AppLayout.wide;
    final gutter = AppLayout.gutterFor(width);

    final searchField = TextField(
      controller: _search,
      textInputAction: TextInputAction.search,
      decoration: InputDecoration(
        labelText: 'Search by title',
        prefixIcon: const Icon(Icons.search),
        suffixIcon: filter.q.isEmpty && _search.text.isEmpty
            ? null
            : IconButton(
                tooltip: 'Clear search',
                icon: const Icon(Icons.clear),
                onPressed: () {
                  _search.clear();
                  notifier.set(filter.copyWith(q: ''));
                },
              ),
        isDense: true,
      ),
      onSubmitted: (v) => notifier.set(filter.copyWith(q: v)),
    );
    final competitionField = DropdownButtonFormField<int?>(
      isExpanded: true,
      initialValue: filter.competitionId,
      decoration: const InputDecoration(labelText: 'Competition', isDense: true),
      items: [
        const DropdownMenuItem<int?>(value: null, child: Text('All')),
        for (final c in competitions)
          DropdownMenuItem<int?>(value: c.id, child: Text(c.title, overflow: TextOverflow.ellipsis)),
      ],
      onChanged: (v) => notifier.set(filter.copyWith(competitionId: () => v)),
    );
    final ratingField = DropdownButtonFormField<SubmissionRating?>(
      isExpanded: true,
      initialValue: filter.rating,
      decoration: const InputDecoration(labelText: 'Rating', isDense: true),
      items: [
        const DropdownMenuItem<SubmissionRating?>(value: null, child: Text('All')),
        for (final r in SubmissionRating.values)
          DropdownMenuItem<SubmissionRating?>(value: r, child: Text(r.label, overflow: TextOverflow.ellipsis)),
      ],
      onChanged: (v) => notifier.set(filter.copyWith(rating: () => v)),
    );

    // Graded competitions the student entered (any filter) → awards section
    final graded = <int, String>{
      for (final s in ref.watch(allMySubmissionsProvider).value ?? const <SubmissionListItem>[])
        if (s.competitionStatus == CompetitionStatus.graded) s.competitionId: s.competitionTitle,
    };

    // The entries matching the filters; only this part reloads when a filter changes
    Widget entries(List<SubmissionListItem> items) {
      if (items.isEmpty) {
        return Padding(
          padding: const EdgeInsets.only(top: AppSpacing.xl),
          child: EmptyState(
            title: filter.isActive ? 'No entries match your filters' : 'No entries yet',
            message: filter.isActive ? null : 'Enter an open competition and your work will appear here.',
            actionLabel: filter.isActive ? null : 'Browse competitions',
            onAction: filter.isActive ? null : () => context.go('/student/competitions'),
          ),
        );
      }
      if (wide) {
        // Two columns of entries (the grid has no side padding of its own here: the list already has it)
        return Padding(
          padding: const EdgeInsets.only(top: AppSpacing.lg),
          child: AdaptiveTileGrid(
            minTileWidth: 440,
            maxColumns: 2,
            spacing: AppSpacing.xl,
            padding: EdgeInsets.zero,
            children: [for (final item in items) EntryTile(item: item)],
          ),
        );
      }
      // Room between the filters and the first entry, like a wall label under the frame
      return Column(
        children: [
          const SizedBox(height: AppSpacing.lg),
          const Divider(height: 1),
          for (final item in items) ...[
            EntryTile(item: item),
            const Divider(height: 1),
          ],
        ],
      );
    }

    return TabScaffold(
      title: 'My entries',
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: EdgeInsets.fromLTRB(gutter, AppSpacing.md, gutter, AppSpacing.xl),
          children: [
            // The filters stay put (and keep the keyboard) while the list under them reloads.
            // One row of filters on wide screens; on a phone the search sits above the two selectors
            if (wide)
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(flex: 3, child: searchField),
                  const SizedBox(width: AppSpacing.md),
                  Expanded(flex: 2, child: competitionField),
                  const SizedBox(width: AppSpacing.md),
                  Expanded(flex: 2, child: ratingField),
                ],
              )
            else ...[
              searchField,
              const SizedBox(height: AppSpacing.md),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(child: competitionField),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(child: ratingField),
                ],
              ),
            ],
            if (filter.isActive)
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton(
                  onPressed: () {
                    _search.clear();
                    notifier.set(const SubmissionFilter());
                  },
                  child: const Text('Clear filters'),
                ),
              ),
            AsyncValueView(
              value: list,
              onRetry: () => ref.invalidate(mySubmissionsProvider),
              placeholder: Placeholders.mySubmissions,
              data: entries,
            ),
            if (graded.isNotEmpty) ...[
              const SectionHeader('Awards',
                  subtitle: 'Prizes of the competitions you entered',
                  padding: EdgeInsets.fromLTRB(0, AppSpacing.xl, 0, AppSpacing.md)),
              for (final e in graded.entries) ...[
                Text(e.value, style: Theme.of(context).textTheme.titleSmall),
                AwardResultsList(competitionId: e.key),
                const SizedBox(height: AppSpacing.sm),
              ],
            ],
          ],
        ),
      ),
    );
  }
}

/// One entry of the list: opens the detail. Looks up the award once the results are published.
class EntryTile extends ConsumerWidget {
  const EntryTile({super.key, required this.item});

  final SubmissionListItem item;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    AwardResult? award;
    if (item.competitionStatus == CompetitionStatus.graded) {
      award = awardOf(item, ref.watch(competitionAwardsProvider(item.competitionId)).value ?? const []);
    }
    return SubmissionRow(
      item: item,
      awardName: award?.awardName,
      note: item.canEdit ? 'You can still edit this entry' : null,
      onTap: () => context.push('/student/submissions/${item.id}'),
    );
  }
}
