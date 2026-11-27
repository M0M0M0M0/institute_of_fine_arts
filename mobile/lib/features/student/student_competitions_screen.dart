import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../shared/shared.dart';
import '../../shared/tab_scaffold.dart';
import '../competitions/competitions.dart';
import '../submissions/submissions.dart';

enum _Segment { open, upcoming, results }

extension on _Segment {
  String get label => switch (this) {
        _Segment.open => 'Open',
        _Segment.upcoming => 'Upcoming',
        _Segment.results => 'Results',
      };

  bool includes(CompetitionStatus status) => switch (this) {
        _Segment.open => status == CompetitionStatus.ongoing,
        _Segment.upcoming => status == CompetitionStatus.upcoming,
        _Segment.results => status == CompetitionStatus.closed || status == CompetitionStatus.graded,
      };
}

/// M-S2 Competitions tab for students: Open · Upcoming · Results, as competition cards.
/// Open competitions the student has not entered get a "Submit" button; entered ones "View my entry".
class StudentCompetitionsScreen extends ConsumerStatefulWidget {
  const StudentCompetitionsScreen({super.key});

  @override
  ConsumerState<StudentCompetitionsScreen> createState() => _StudentCompetitionsScreenState();
}

class _StudentCompetitionsScreenState extends ConsumerState<StudentCompetitionsScreen> {
  _Segment _segment = _Segment.open;

  /// The card and, under it, what the student can do with this competition.
  Widget _entry(BuildContext context, CompetitionSummary c, Map<int, int> entryOf) {
    final entryId = entryOf[c.id];
    final Widget? action = c.status == CompetitionStatus.ongoing
        ? (entryId == null
            ? FilledButton(
                onPressed: () => context.push('/student/submissions/new?competitionId=${c.id}'),
                child: const Text('SUBMIT'),
              )
            : OutlinedButton(
                onPressed: () => context.push('/student/submissions/$entryId'),
                child: const Text('VIEW MY ENTRY'),
              ))
        : (entryId != null
            ? OutlinedButton(
                onPressed: () => context.push('/student/submissions/$entryId'),
                child: const Text('VIEW MY ENTRY'),
              )
            : null);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        CompetitionCard(competition: c, onTap: () => context.push('/competitions/${c.id}')),
        if (action != null) ...[const SizedBox(height: AppSpacing.md), action],
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final list = ref.watch(competitionListProvider);
    // Entries of the student, to tell "Submit" from "View my entry" with one request
    final mine = ref.watch(allMySubmissionsProvider).value ?? const <SubmissionListItem>[];
    final entryOf = {for (final s in mine) s.competitionId: s.id};
    final width = MediaQuery.sizeOf(context).width;
    final wide = width >= AppLayout.wide;
    final gutter = AppLayout.gutterFor(width);
    final all = list.value;

    return TabScaffold(
      title: 'Competitions',
      body: Column(
        children: [
          Padding(
            padding: EdgeInsets.symmetric(horizontal: gutter),
            child: SegmentTabs<_Segment>(
              values: _Segment.values,
              selected: _segment,
              expand: !wide,
              labelOf: (s) => s.label,
              countOf: all == null ? null : (s) => all.where((c) => s.includes(c.status)).length,
              onSelected: (s) => setState(() => _segment = s),
            ),
          ),
          const SizedBox(height: AppSpacing.lg),
          Expanded(
            child: AsyncValueView(
              value: list,
              onRetry: () => ref.invalidate(competitionListProvider),
              placeholder: Placeholders.competitions,
              data: (all) {
                final items = all.where((c) => _segment.includes(c.status)).toList()
                  ..sort((a, b) => _segment == _Segment.results
                      ? b.endDate.compareTo(a.endDate)
                      : a.startDate.compareTo(b.startDate));
                return RefreshIndicator(
                  onRefresh: () async {
                    ref.invalidate(allMySubmissionsProvider);
                    ref.invalidate(competitionListProvider);
                    await ref.read(competitionListProvider.future);
                  },
                  child: items.isEmpty
                      ? ListView(
                          physics: const AlwaysScrollableScrollPhysics(),
                          children: [
                            const SizedBox(height: AppSpacing.xxl),
                            EmptyState(
                              title: switch (_segment) {
                                _Segment.open => 'No competition is open right now',
                                _Segment.upcoming => 'Nothing is scheduled yet',
                                _Segment.results => 'No results yet',
                              },
                              icon: Icons.palette_outlined,
                            ),
                          ],
                        )
                      : wide
                          // Columns on wide screens: a single card would otherwise stretch across the page
                          ? ListView(
                              physics: const AlwaysScrollableScrollPhysics(),
                              padding: const EdgeInsets.only(bottom: AppSpacing.xl),
                              children: [
                                AdaptiveTileGrid(
                                  minTileWidth: 300,
                                  maxColumns: 3,
                                  spacing: AppSpacing.xl,
                                  children: [for (final c in items) _entry(context, c, entryOf)],
                                ),
                              ],
                            )
                          : ListView.separated(
                              physics: const AlwaysScrollableScrollPhysics(),
                              padding: EdgeInsets.fromLTRB(gutter, AppSpacing.sm, gutter, AppSpacing.xl),
                              itemCount: items.length,
                              separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.xl),
                              itemBuilder: (context, i) => StaggerIn(index: i, child: _entry(context, items[i], entryOf)),
                            ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
