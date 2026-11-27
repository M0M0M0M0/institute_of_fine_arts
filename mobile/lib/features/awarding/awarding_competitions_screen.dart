import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import 'awarding.dart';
import 'awarding_widgets.dart';

/// M-T4 (1/2): the staff "Awards" tab: closed or published competitions (UC-06 step 1).
class AwardingCompetitionsTab extends ConsumerWidget {
  const AwardingCompetitionsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final competitions = ref.watch(awardingCompetitionsProvider);
    final width = MediaQuery.sizeOf(context).width;
    final gutter = AppLayout.gutterFor(width);
    return AsyncValueView(
      value: competitions,
      onRetry: () => ref.invalidate(awardingCompetitionsProvider),
      placeholder: Placeholders.awardingCompetitions,
      data: (items) => RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(awardingCompetitionsProvider);
          await ref.read(awardingCompetitionsProvider.future);
        },
        child: items.isEmpty
            ? ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                children: const [
                  SizedBox(height: AppSpacing.xl),
                  EmptyState(
                    icon: Icons.emoji_events_outlined,
                    title: 'No competitions to award yet',
                    message: 'Competitions appear here once their submission deadline has passed.',
                  ),
                ],
              )
            : width >= AppLayout.wide
                // Columns on wide screens
                ? ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.only(top: AppSpacing.sm, bottom: AppSpacing.xl),
                    children: [
                      AdaptiveTileGrid(
                        minTileWidth: 300,
                        maxColumns: 3,
                        spacing: AppSpacing.xl,
                        children: [for (final item in items) _CompetitionTile(item: item)],
                      ),
                    ],
                  )
                : ListView.separated(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: EdgeInsets.fromLTRB(gutter, AppSpacing.md, gutter, AppSpacing.xl),
                    itemCount: items.length,
                    separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.xl),
                    itemBuilder: (context, i) => StaggerIn(index: i, child: _CompetitionTile(item: items[i])),
                  ),
      ),
    );
  }
}

class _CompetitionTile extends StatelessWidget {
  const _CompetitionTile({required this.item});

  final AwardingCompetition item;

  @override
  Widget build(BuildContext context) {
    return CompetitionProgressCard(
      competitionId: item.id,
      title: item.title,
      hasCover: item.hasCover,
      status: item.status,
      dateLine: 'Closed ${formatDate(item.endDate)}',
      progressLabel: 'Award slots filled',
      progressValue: item.issuedSlots,
      progressTotal: item.totalSlots,
      progressColor: AppColors.brass,
      onTap: () => context.push('/staff/awarding/${item.id}'),
      footer: item.adjustedAt == null ? null : AdjustedLabel(item.adjustedAt!),
    );
  }
}
