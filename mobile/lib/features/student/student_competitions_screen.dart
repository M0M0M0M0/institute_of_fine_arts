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

/// M-S2 Competitions tab for students: Open · Upcoming · Results, as competition cards.
/// Open competitions the student has not entered get a "Submit" button; entered ones "View my entry".
class StudentCompetitionsScreen extends ConsumerStatefulWidget {
  const StudentCompetitionsScreen({super.key});

  @override
  ConsumerState<StudentCompetitionsScreen> createState() => _StudentCompetitionsScreenState();
}

class _StudentCompetitionsScreenState extends ConsumerState<StudentCompetitionsScreen> {
  _Segment _segment = _Segment.open;

  bool _matches(CompetitionSummary c) => switch (_segment) {
        _Segment.open => c.status == CompetitionStatus.ongoing,
        _Segment.upcoming => c.status == CompetitionStatus.upcoming,
        _Segment.results => c.status == CompetitionStatus.closed || c.status == CompetitionStatus.graded,
      };

  @override
  Widget build(BuildContext context) {
    final list = ref.watch(competitionListProvider);
    // Entries of the student, to tell "Submit" from "View my entry" with one request
    final mine = ref.watch(allMySubmissionsProvider).value ?? const <SubmissionListItem>[];
    final entryOf = {for (final s in mine) s.competitionId: s.id};

    return TabScaffold(
      title: 'Competitions',
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.md, AppSpacing.gutter, AppSpacing.sm),
            child: SizedBox(
              width: double.infinity,
              child: SegmentedButton<_Segment>(
                showSelectedIcon: false,
                segments: const [
                  ButtonSegment(value: _Segment.open, label: Text('Open')),
                  ButtonSegment(value: _Segment.upcoming, label: Text('Upcoming')),
                  ButtonSegment(value: _Segment.results, label: Text('Results')),
                ],
                selected: {_segment},
                onSelectionChanged: (s) => setState(() => _segment = s.first),
              ),
            ),
          ),
          Expanded(
            child: AsyncValueView(
              value: list,
              onRetry: () => ref.invalidate(competitionListProvider),
              data: (all) {
                final items = all.where(_matches).toList()
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
                      : ListView.separated(
                          physics: const AlwaysScrollableScrollPhysics(),
                          padding: const EdgeInsets.fromLTRB(
                              AppSpacing.gutter, AppSpacing.sm, AppSpacing.gutter, AppSpacing.xl),
                          itemCount: items.length,
                          separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.md),
                          itemBuilder: (context, i) {
                            final c = items[i];
                            final entryId = entryOf[c.id];
                            return Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                CompetitionCard(competition: c, onTap: () => context.push('/competitions/${c.id}')),
                                if (c.status == CompetitionStatus.ongoing)
                                  entryId == null
                                      ? FilledButton(
                                          onPressed: () =>
                                              context.push('/student/submissions/new?competitionId=${c.id}'),
                                          child: const Text('Submit'),
                                        )
                                      : OutlinedButton(
                                          onPressed: () => context.push('/student/submissions/$entryId'),
                                          child: const Text('View my entry'),
                                        )
                                else if (entryId != null)
                                  OutlinedButton(
                                    onPressed: () => context.push('/student/submissions/$entryId'),
                                    child: const Text('View my entry'),
                                  ),
                              ],
                            );
                          },
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
