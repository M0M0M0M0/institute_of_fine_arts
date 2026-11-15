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

  CompetitionStatus get status => switch (this) {
        CompetitionSection.open => CompetitionStatus.ongoing,
        CompetitionSection.upcoming => CompetitionStatus.upcoming,
        CompetitionSection.results => CompetitionStatus.graded,
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
    final list = all.where((c) => c.status == _section.status).toList();
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
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.md, AppSpacing.gutter, AppSpacing.sm),
          child: SizedBox(
            width: double.infinity,
            child: SegmentedButton<CompetitionSection>(
              showSelectedIcon: false,
              segments: [
                for (final s in CompetitionSection.values) ButtonSegment(value: s, label: Text(s.label)),
              ],
              selected: {_section},
              onSelectionChanged: (v) => setState(() => _section = v.first),
            ),
          ),
        ),
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
                    : ListView.separated(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.all(AppSpacing.gutter),
                        itemCount: items.length,
                        separatorBuilder: (context, i) => const SizedBox(height: AppSpacing.md),
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
