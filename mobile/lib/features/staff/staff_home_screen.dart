import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import '../../shared/tab_scaffold.dart';
import '../auth/auth.dart';
import 'dashboard.dart';

/// M-T1: the staff to-do overview. Every card leads straight to the screen that handles it.
class StaffHomeScreen extends ConsumerWidget {
  const StaffHomeScreen({super.key});

  static String greeting(DateTime now) {
    if (now.hour < 12) return 'Good morning';
    if (now.hour < 18) return 'Good afternoon';
    return 'Good evening';
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = currentUserOf(ref.watch(authControllerProvider));
    final dashboard = ref.watch(staffDashboardProvider);
    return TabScaffold(
      title: 'Home',
      body: AsyncValueView(
        value: dashboard,
        onRetry: () => ref.invalidate(staffDashboardProvider),
        data: (d) => RefreshIndicator(
          onRefresh: () async {
            ref.invalidate(staffDashboardProvider);
            await ref.read(staffDashboardProvider.future);
          },
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.only(bottom: AppSpacing.xl),
            children: [
              _Greeting(name: user?.firstName, data: d),
              if (d.isEmpty)
                const Padding(
                  padding: EdgeInsets.only(top: AppSpacing.xl),
                  child: EmptyState(
                    icon: Icons.check_circle_outline,
                    title: 'Nothing waiting',
                    message: 'No grading, publishing or exhibition work needs you right now.',
                  ),
                ),
              if (d.toGrade.isNotEmpty) ...[
                const SectionHeader('To grade', subtitle: 'Closed competitions with ungraded entries'),
                for (final item in d.toGrade) _ToGradeCard(item: item),
              ],
              if (d.toPublish.isNotEmpty) ...[
                const SectionHeader('Ready to publish', subtitle: 'Every entry is graded'),
                for (final item in d.toPublish) _ToPublishCard(item: item),
              ],
              if (d.pendingProposals > 0 || d.unpaidSales > 0) ...[
                const SectionHeader('Exhibitions'),
                if (d.pendingProposals > 0)
                  _CountCard(
                    icon: Icons.hourglass_top_outlined,
                    count: d.pendingProposals,
                    title: d.pendingProposals == 1 ? 'Proposal waiting for review' : 'Proposals waiting for review',
                    onTap: () => context.go('/staff/exhibitions?show=pending'),
                  ),
                if (d.unpaidSales > 0)
                  _CountCard(
                    icon: Icons.payments_outlined,
                    count: d.unpaidSales,
                    title: d.unpaidSales == 1 ? 'Sale to pay out to the student' : 'Sales to pay out to students',
                    onTap: () => context.go('/staff/exhibitions?show=unpaid'),
                  ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _Greeting extends StatelessWidget {
  const _Greeting({required this.name, required this.data});

  final String? name;
  final StaffDashboard data;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final tasks = data.toGrade.length +
        data.toPublish.length +
        (data.pendingProposals > 0 ? 1 : 0) +
        (data.unpaidSales > 0 ? 1 : 0);
    return Container(
      color: AppColors.mist,
      padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.lg, AppSpacing.gutter, AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Semantics(
            header: true,
            child: Text(
              '${StaffHomeScreen.greeting(DateTime.now())}${name == null ? '' : ', $name'}',
              style: text.headlineSmall,
            ),
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            tasks == 0
                ? 'You are all caught up.'
                : (tasks == 1 ? '1 thing needs your attention.' : '$tasks things need your attention.'),
            style: text.bodyMedium!.copyWith(color: AppColors.muted),
          ),
        ],
      ),
    );
  }
}

class _CoverThumb extends StatelessWidget {
  const _CoverThumb({required this.competitionId, required this.hasCover, required this.label});

  final int competitionId;
  final bool hasCover;
  final String label;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 72,
      height: 72,
      child: ClipRRect(
        borderRadius: AppRadius.smAll,
        child: hasCover
            ? PublicImage(url: ApiUrls.competitionCover(competitionId), semanticLabel: 'Cover of $label')
            : const ImagePlaceholder(icon: Icons.palette_outlined),
      ),
    );
  }
}

class _ToGradeCard extends StatelessWidget {
  const _ToGradeCard({required this.item});

  final ToGradeItem item;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: AppSpacing.gutter, vertical: AppSpacing.xs),
      child: InkWell(
        onTap: () => context.push('/staff/competitions/${item.competitionId}/submissions'),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _CoverThumb(competitionId: item.competitionId, hasCover: item.hasCover, label: item.title),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(item.title, style: text.titleMedium, maxLines: 2, overflow: TextOverflow.ellipsis),
                    if (item.endDate != null) Text('Closed ${formatDate(item.endDate!)}', style: text.bodySmall),
                    const SizedBox(height: AppSpacing.sm),
                    ProgressBarRow(label: '${item.remaining} left to grade', value: item.evaluated, total: item.total),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right, color: AppColors.muted),
            ],
          ),
        ),
      ),
    );
  }
}

class _ToPublishCard extends StatelessWidget {
  const _ToPublishCard({required this.item});

  final ToPublishItem item;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final empty = item.emptySlots;
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: AppSpacing.gutter, vertical: AppSpacing.xs),
      child: InkWell(
        onTap: () => context.push('/staff/awarding/${item.competitionId}'),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Row(
            children: [
              _CoverThumb(competitionId: item.competitionId, hasCover: item.hasCover, label: item.title),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(item.title, style: text.titleMedium, maxLines: 2, overflow: TextOverflow.ellipsis),
                    const SizedBox(height: 2),
                    Text(
                      empty == 0
                          ? 'All award slots are filled'
                          : '$empty award ${empty == 1 ? 'slot' : 'slots'} still empty',
                      style: text.bodySmall,
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right, color: AppColors.muted),
            ],
          ),
        ),
      ),
    );
  }
}

class _CountCard extends StatelessWidget {
  const _CountCard({required this.icon, required this.count, required this.title, required this.onTap});

  final IconData icon;
  final int count;
  final String title;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: AppSpacing.gutter, vertical: AppSpacing.xs),
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Row(
            children: [
              Icon(icon, color: AppColors.brassText),
              const SizedBox(width: AppSpacing.md),
              Text('$count', style: text.headlineSmall),
              const SizedBox(width: AppSpacing.md),
              Expanded(child: Text(title, style: text.bodyLarge)),
              const Icon(Icons.chevron_right, color: AppColors.muted),
            ],
          ),
        ),
      ),
    );
  }
}
