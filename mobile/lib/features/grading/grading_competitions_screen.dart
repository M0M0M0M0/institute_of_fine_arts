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

  final GradingCompetition item;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final ongoing = item.status == CompetitionStatus.ongoing;
    return Card(
      clipBehavior: Clip.antiAlias,
      margin: const EdgeInsets.only(bottom: AppSpacing.md),
      child: InkWell(
        onTap: () => context.push('${gradingBase(context)}/competitions/${item.id}/submissions'),
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
                  Text('${ongoing ? 'Closes' : 'Closed'} ${formatDate(item.endDate)}', style: text.bodySmall),
                  const SizedBox(height: AppSpacing.sm),
                  ProgressBarRow(
                    label: 'Graded',
                    value: item.evaluatedCount,
                    total: item.submissionCount,
                    color: AppColors.statusOngoing,
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
