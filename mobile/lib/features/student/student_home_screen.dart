import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import '../../shared/tab_scaffold.dart';
import '../auth/auth.dart';
import '../competitions/competitions.dart';
import '../submissions/submission_widgets.dart';
import '../submissions/submissions.dart';
import 'dashboard.dart';

/// "Good morning" / "Good afternoon" / "Good evening" for the given time.
String greetingFor(DateTime now) {
  final h = now.hour;
  if (h < 12) return 'Good morning';
  if (h < 18) return 'Good afternoon';
  return 'Good evening';
}

/// M-S1 Student home: what to do next (open competitions, editable entries, new results,
/// exhibition tasks). Pull to refresh.
class StudentHomeScreen extends ConsumerWidget {
  const StudentHomeScreen({super.key, this.now});

  /// Clock override for tests.
  final DateTime? now;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = currentUserOf(ref.watch(authControllerProvider));
    final dashboard = ref.watch(studentDashboardProvider);
    final text = Theme.of(context).textTheme;
    final width = MediaQuery.sizeOf(context).width;
    final wide = width >= AppLayout.wide;
    final gutter = AppLayout.gutterFor(width);
    final today = now ?? DateTime.now();

    // A competition card with the "Submit" button under it
    Widget openCard(CompetitionSummary c, {double? cardWidth}) => SizedBox(
          width: cardWidth,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              CompetitionCard(competition: c, onTap: () => context.push('/competitions/${c.id}')),
              const SizedBox(height: AppSpacing.md),
              FilledButton(
                onPressed: () => context.push('/student/submissions/new?competitionId=${c.id}'),
                child: const Text('SUBMIT'),
              ),
            ],
          ),
        );

    return TabScaffold(
      title: 'Home',
      showHeading: false,
      body: AsyncValueView(
        value: dashboard,
        onRetry: () => ref.invalidate(studentDashboardProvider),
        data: (d) => RefreshIndicator(
          onRefresh: () async {
            ref.invalidate(studentDashboardProvider);
            ref.invalidate(competitionListProvider);
            await ref.read(studentDashboardProvider.future);
          },
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.only(bottom: AppSpacing.xl),
            children: [
              // The greeting is the title of the page: today's date as the small line, the greeting large and light
              Padding(
                padding: EdgeInsets.fromLTRB(gutter, wide ? AppSpacing.xl : AppSpacing.lg, gutter, 0),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const SizedBox(width: 28, height: 2, child: ColoredBox(color: AppColors.brass)),
                    const SizedBox(height: AppSpacing.md),
                    Text(
                      formatDate(today).toUpperCase(),
                      style: text.labelSmall!.copyWith(color: AppColors.muted, letterSpacing: 1.8),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    Semantics(
                      header: true,
                      child: Text(
                        '${greetingFor(today)}, ${user?.firstName ?? 'student'}',
                        style: wide ? text.displaySmall : text.headlineLarge,
                      ),
                    ),
                  ],
                ),
              ),
              if (d.isEmpty)
                const Padding(
                  padding: EdgeInsets.only(top: AppSpacing.xxl),
                  child: EmptyState(
                    title: "You're all caught up",
                    message: 'Nothing needs your attention right now.',
                    icon: Icons.check_circle_outline,
                  ),
                ),
              if (d.openToEnter.isNotEmpty) ...[
                const SectionHeader('Open for entries', subtitle: 'You have not submitted to these yet'),
                if (wide)
                  AdaptiveTileGrid(
                    minTileWidth: 300,
                    maxColumns: 3,
                    spacing: AppSpacing.xl,
                    children: [for (final c in d.openToEnter) openCard(c)],
                  )
                else
                  SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    padding: EdgeInsets.symmetric(horizontal: gutter),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        for (final c in d.openToEnter)
                          Padding(
                            padding: const EdgeInsets.only(right: AppSpacing.md),
                            // A lone card fills the screen; with several, the next one peeks out to invite a swipe
                            child: openCard(c, cardWidth: d.openToEnter.length == 1 ? width - 2 * gutter : 280),
                          ),
                      ],
                    ),
                  ),
              ],
              if (d.editable.isNotEmpty) ...[
                const SectionHeader('Still editable', subtitle: 'Change your entry until the deadline'),
                _EntryList(items: d.editable, editable: true),
              ],
              if (d.newResults.isNotEmpty) ...[
                const SectionHeader('New results'),
                _EntryList(items: d.newResults),
              ],
              if (d.pendingProposals > 0 || d.unpaidSales > 0) ...[
                const SectionHeader('Exhibitions'),
                // No card chrome: two hairlines frame the notice, like a line on a wall label
                Padding(
                  padding: EdgeInsets.symmetric(horizontal: gutter),
                  child: InkWell(
                    onTap: () => context.go('/student/exhibitions'),
                    child: DecoratedBox(
                      decoration: const BoxDecoration(
                        border: Border.symmetric(horizontal: BorderSide(color: AppColors.line)),
                      ),
                      child: Padding(
                        padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
                        child: Row(
                          children: [
                            const Icon(Icons.museum_outlined, color: AppColors.brass),
                            const SizedBox(width: AppSpacing.md),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  if (d.pendingProposals > 0)
                                    Text(
                                      d.pendingProposals == 1
                                          ? '1 proposal waiting for review'
                                          : '${d.pendingProposals} proposals waiting for review',
                                      style: text.bodyLarge,
                                    ),
                                  if (d.unpaidSales > 0)
                                    Text(
                                      d.unpaidSales == 1
                                          ? '1 sold work, payment not received yet'
                                          : '${d.unpaidSales} sold works, payment not received yet',
                                      style: text.bodyLarge,
                                    ),
                                ],
                              ),
                            ),
                            const Icon(Icons.arrow_forward, size: 18, color: AppColors.brassText),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// Rows of entries with a thumbnail; [editable] shows the competition deadline instead of the state.
class _EntryList extends ConsumerWidget {
  const _EntryList({required this.items, this.editable = false});

  final List<SubmissionListItem> items;
  final bool editable;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final competitions = ref.watch(competitionListProvider).value ?? const <CompetitionSummary>[];
    String? closes(SubmissionListItem s) {
      for (final c in competitions) {
        if (c.id == s.competitionId) return 'Closes ${formatDate(c.endDate)}';
      }
      return null;
    }

    final rows = [
      for (final s in items)
        SubmissionRow(
          item: s,
          showState: !editable,
          note: editable ? closes(s) : null,
          onTap: () => context.push('/student/submissions/${s.id}'),
        ),
    ];
    // Two columns of rows on wide screens, a plain list on a phone
    if (MediaQuery.sizeOf(context).width >= AppLayout.wide) {
      return AdaptiveTileGrid(minTileWidth: 440, maxColumns: 2, spacing: AppSpacing.xl, children: rows);
    }
    return Padding(
      padding: EdgeInsets.symmetric(horizontal: AppLayout.gutterFor(MediaQuery.sizeOf(context).width)),
      child: Column(children: rows),
    );
  }
}
