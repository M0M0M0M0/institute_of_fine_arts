import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import '../competitions/competitions.dart';
import 'awarding.dart';
import 'awarding_widgets.dart';

/// M-T4 (1/2): the staff "Awards" tab: closed or published competitions (UC-06 step 1).
class AwardingCompetitionsTab extends ConsumerWidget {
  const AwardingCompetitionsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final competitions = ref.watch(awardingCompetitionsProvider);
    return AsyncValueView(
      value: competitions,
      onRetry: () => ref.invalidate(awardingCompetitionsProvider),
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
            : ListView.builder(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(AppSpacing.gutter),
                itemCount: items.length,
                itemBuilder: (context, i) => _CompetitionTile(item: items[i]),
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
    final text = Theme.of(context).textTheme;
    final graded = item.status == CompetitionStatus.graded;
    final pending = item.submissionCount - item.evaluatedCount;
    return Card(
      clipBehavior: Clip.antiAlias,
      margin: const EdgeInsets.only(bottom: AppSpacing.md),
      child: InkWell(
        onTap: () => context.push('/staff/awarding/${item.id}'),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            AspectRatio(
              aspectRatio: 16 / 7,
              child: item.hasCover
                  ? PublicImage(url: ApiUrls.competitionCover(item.id), semanticLabel: 'Cover of ${item.title}')
                  : const ImagePlaceholder(icon: Icons.palette_outlined),
            ),
            Padding(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(child: Text(item.title, style: text.titleMedium)),
                      const SizedBox(width: AppSpacing.sm),
                      StatusChip(item.status),
                    ],
                  ),
                  const SizedBox(height: 2),
                  Text('Closed ${formatDate(item.endDate)}', style: text.bodySmall),
                  const SizedBox(height: AppSpacing.sm),
                  ProgressBarRow(
                    label: 'Award slots filled',
                    value: item.issuedSlots,
                    total: item.totalSlots,
                    color: AppColors.brass,
                  ),
                  // Not published yet: remind how many entries are still ungraded (BR-08)
                  if (!graded && pending > 0)
                    Padding(
                      padding: const EdgeInsets.only(top: AppSpacing.sm),
                      child: Text(
                        '$pending ${pending == 1 ? 'entry is' : 'entries are'} not graded yet, so results cannot be published.',
                        style: text.bodySmall!.copyWith(color: AppColors.danger),
                      ),
                    ),
                  if (item.adjustedAt != null) AdjustedLabel(item.adjustedAt!),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
