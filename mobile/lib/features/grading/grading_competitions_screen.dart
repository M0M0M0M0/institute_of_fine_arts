import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import '../competitions/competitions.dart';
import 'grading.dart';

/// M-T2 (1/2): the staff "Grading" tab: pick a competition and see the grading progress (UC-05 step 1).
class GradingCompetitionsTab extends ConsumerWidget {
  const GradingCompetitionsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final competitions = ref.watch(gradingCompetitionsProvider);
    final width = MediaQuery.sizeOf(context).width;
    final gutter = AppLayout.gutterFor(width);
    return AsyncValueView(
      value: competitions,
      onRetry: () => ref.invalidate(gradingCompetitionsProvider),
      data: (items) => RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(gradingCompetitionsProvider);
          await ref.read(gradingCompetitionsProvider.future);
        },
        child: items.isEmpty
            ? ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                children: const [
                  SizedBox(height: AppSpacing.xl),
                  EmptyState(
                    icon: Icons.rate_review_outlined,
                    title: 'No competitions to grade',
                    message: 'Competitions appear here once they are open or closed.',
                  ),
                ],
              )
            : width >= AppLayout.wide
            // Columns on wide screens
            ? ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.only(
                  top: AppSpacing.sm,
                  bottom: AppSpacing.xl,
                ),
                children: [
                  AdaptiveTileGrid(
                    minTileWidth: 300,
                    maxColumns: 3,
                    spacing: AppSpacing.xl,
                    children: [
                      for (final item in items) _CompetitionTile(item: item),
                    ],
                  ),
                ],
              )
            : ListView.separated(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: EdgeInsets.fromLTRB(
                  gutter,
                  AppSpacing.md,
                  gutter,
                  AppSpacing.xl,
                ),
                itemCount: items.length,
                separatorBuilder: (_, _) =>
                    const SizedBox(height: AppSpacing.xl),
                itemBuilder: (context, i) => _CompetitionTile(item: items[i]),
              ),
      ),
    );
  }
}

class _CompetitionTile extends StatelessWidget {
  const _CompetitionTile({required this.item});

  final GradingCompetition item;

  @override
  Widget build(BuildContext context) {
    final ongoing = item.status == CompetitionStatus.ongoing;
    return CompetitionProgressCard(
      competitionId: item.id,
      title: item.title,
      hasCover: item.hasCover,
      status: item.status,
      dateLine: '${ongoing ? 'Closes' : 'Closed'} ${formatDate(item.endDate)}',
      progressLabel: 'Graded',
      progressValue: item.evaluatedCount,
      progressTotal: item.submissionCount,
      progressColor: AppColors.statusOngoing,
      onTap: () => context.push(
        '${gradingBase(context)}/competitions/${item.id}/submissions',
      ),
    );
  }
}
