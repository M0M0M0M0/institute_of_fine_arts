import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../shared/shared.dart';
import '../../shared/tab_scaffold.dart';
import '../submissions/submissions.dart';
import 'competitions.dart';

/// The three lists of the Competitions tab.
enum CompetitionSection { open, upcoming, results }

extension on CompetitionSection {
  String get label => switch (this) {
        CompetitionSection.open => 'Open',
        CompetitionSection.upcoming => 'Upcoming',
        CompetitionSection.results => 'Results',
      };

  /// Results also lists CLOSED competitions (deadline passed, judges at work) so none disappears from the tab.
  bool includes(CompetitionStatus status) => switch (this) {
        CompetitionSection.open => status == CompetitionStatus.ongoing,
        CompetitionSection.upcoming => status == CompetitionStatus.upcoming,
        CompetitionSection.results => status == CompetitionStatus.closed || status == CompetitionStatus.graded,
      };

  String get emptyTitle => switch (this) {
        CompetitionSection.open => 'No competition is open right now',
        CompetitionSection.upcoming => 'No upcoming competitions yet',
        CompetitionSection.results => 'No results published yet',
      };
}

/// M-G2: competitions in three segments (Open, Upcoming, Results).
class CompetitionsScreen extends StatelessWidget {
  const CompetitionsScreen({super.key});

  @override
  Widget build(BuildContext context) => const TabScaffold(title: 'Competitions', body: CompetitionsBody());
}

/// Body of [CompetitionsScreen] (no app bar) so other roles can reuse it inside their own shell.
class CompetitionsBody extends ConsumerStatefulWidget {
  const CompetitionsBody({super.key});

  @override
  ConsumerState<CompetitionsBody> createState() => _CompetitionsBodyState();
}

class _CompetitionsBodyState extends ConsumerState<CompetitionsBody> {
  CompetitionSection _section = CompetitionSection.open;

  List<CompetitionSummary> _filter(List<CompetitionSummary> all) {
    final list = all.where((c) => _section.includes(c.status)).toList();
    switch (_section) {
      case CompetitionSection.open:
        list.sort((a, b) => a.endDate.compareTo(b.endDate));
      case CompetitionSection.upcoming:
        list.sort((a, b) => a.startDate.compareTo(b.startDate));
      case CompetitionSection.results:
        list.sort((a, b) => b.endDate.compareTo(a.endDate));
    }
    return list;
  }

  @override
  Widget build(BuildContext context) {
    final competitions = ref.watch(competitionListProvider);
    final width = MediaQuery.sizeOf(context).width;
    final wide = width >= AppLayout.wide;
    final gutter = AppLayout.gutterFor(width);
    final all = competitions.value;
    return Column(
      children: [
        Padding(
          padding: EdgeInsets.symmetric(horizontal: gutter),
          child: SegmentTabs<CompetitionSection>(
            values: CompetitionSection.values,
            selected: _section,
            expand: !wide,
            labelOf: (s) => s.label,
            countOf: all == null ? null : (s) => all.where((c) => s.includes(c.status)).length,
            onSelected: (s) => setState(() => _section = s),
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        Expanded(
          child: AsyncValueView(
            value: competitions,
            onRetry: () => ref.invalidate(competitionListProvider),
            data: (all) {
              final items = _filter(all);
              return RefreshIndicator(
                onRefresh: () => ref.refresh(competitionListProvider.future),
                child: items.isEmpty
                    ? ListView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        children: [SizedBox(height: 320, child: EmptyState(title: _section.emptyTitle))],
                      )
                    : wide
                        // A grid of columns: a single card would otherwise stretch across the whole page
                        ? ListView(
                            physics: const AlwaysScrollableScrollPhysics(),
                            padding: const EdgeInsets.only(bottom: AppSpacing.xl),
                            children: [
                              AdaptiveTileGrid(
                                minTileWidth: 300,
                                maxColumns: 3,
                                spacing: AppSpacing.xl,
                                children: [
                                  for (final c in items)
                                    CompetitionCard(competition: c, onTap: () => context.push('/competitions/${c.id}')),
                                ],
                              ),
                            ],
                          )
                        : ListView.separated(
                            physics: const AlwaysScrollableScrollPhysics(),
                            padding: EdgeInsets.fromLTRB(gutter, AppSpacing.sm, gutter, AppSpacing.xl),
                            itemCount: items.length,
                            separatorBuilder: (context, i) => const SizedBox(height: AppSpacing.xl),
                            itemBuilder: (context, i) => CompetitionCard(
                              competition: items[i],
                              onTap: () => context.push('/competitions/${items[i].id}'),
                            ),
                          ),
              );
            },
          ),
        ),
      ],
    );
  }
}
