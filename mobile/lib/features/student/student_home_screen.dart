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

    return TabScaffold(
      title: 'Home',
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
              Padding(
                padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.lg, AppSpacing.gutter, 0),
                child: Semantics(
                  header: true,
                  child: Text('${greetingFor(now ?? DateTime.now())}, ${user?.firstName ?? 'student'}',
                      style: text.headlineMedium),
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
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(horizontal: AppSpacing.gutter),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      for (final c in d.openToEnter)
                        Padding(
                          padding: const EdgeInsets.only(right: AppSpacing.md),
                          child: SizedBox(
                            width: 280,
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                CompetitionCard(competition: c, onTap: () => context.push('/competitions/${c.id}')),
                                FilledButton(
                                  onPressed: () => context.push('/student/submissions/new?competitionId=${c.id}'),
                                  child: const Text('Submit'),
                                ),
                              ],
                            ),
                          ),
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
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: AppSpacing.gutter),
                  child: Card(
                    child: InkWell(
                      onTap: () => context.go('/student/exhibitions'),
                      child: Padding(
                        padding: const EdgeInsets.all(AppSpacing.md),
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
                            const Icon(Icons.chevron_right, color: AppColors.muted),
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

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.gutter),
      child: Column(
        children: [
          for (final s in items)
            SubmissionRow(
              item: s,
              showState: !editable,
              note: editable ? closes(s) : null,
              onTap: () => context.push('/student/submissions/${s.id}'),
            ),
        ],
      ),
    );
  }
}
