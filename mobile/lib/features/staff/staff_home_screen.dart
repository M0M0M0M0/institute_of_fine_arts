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
    final wide = MediaQuery.sizeOf(context).width >= AppLayout.wide;

    // The rows of one section: two columns on wide screens, a plain list on a phone
    Widget rows(List<Widget> children) => wide
        ? AdaptiveTileGrid(minTileWidth: 440, maxColumns: 2, spacing: AppSpacing.xl, children: children)
        : Padding(
            padding: EdgeInsets.symmetric(horizontal: AppLayout.gutterFor(MediaQuery.sizeOf(context).width)),
            child: Column(children: children),
          );

    return TabScaffold(
      title: 'Home',
      showHeading: false,
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
                const SectionHeader('Being graded', subtitle: 'Closed competitions the teachers are grading'),
                rows([for (final item in d.toGrade) _ToGradeRow(item: item)]),
              ],
              if (d.toPublish.isNotEmpty) ...[
                const SectionHeader('Ready to publish', subtitle: 'Every entry is graded'),
                rows([for (final item in d.toPublish) _ToPublishRow(item: item)]),
              ],
              if (d.pendingProposals > 0 || d.unpaidSales > 0) ...[
                const SectionHeader('Exhibitions'),
                rows([
                  if (d.pendingProposals > 0)
                    _CountRow(
                      icon: Icons.hourglass_top_outlined,
                      count: d.pendingProposals,
                      title: d.pendingProposals == 1 ? 'Proposal waiting for review' : 'Proposals waiting for review',
                      onTap: () => context.go('/staff/exhibitions?show=pending'),
                    ),
                  if (d.unpaidSales > 0)
                    _CountRow(
                      icon: Icons.payments_outlined,
                      count: d.unpaidSales,
                      title: d.unpaidSales == 1 ? 'Sale to pay out to the student' : 'Sales to pay out to students',
                      onTap: () => context.go('/staff/exhibitions?show=unpaid'),
                    ),
                ]),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// The greeting is the title of the page: the date as the small line, the greeting large and light, then a
/// sentence about what needs attention.
class _Greeting extends StatelessWidget {
  const _Greeting({required this.name, required this.data});

  final String? name;
  final StaffDashboard data;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final width = MediaQuery.sizeOf(context).width;
    final wide = width >= AppLayout.wide;
    // Grading is the teachers' work, so it is not counted as a task of the staff member
    final tasks = data.toPublish.length +
        (data.pendingProposals > 0 ? 1 : 0) +
        (data.unpaidSales > 0 ? 1 : 0);
    final now = DateTime.now();
    return Padding(
      padding: EdgeInsets.fromLTRB(AppLayout.gutterFor(width), wide ? AppSpacing.xl : AppSpacing.lg,
          AppLayout.gutterFor(width), 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SizedBox(width: 28, height: 2, child: ColoredBox(color: AppColors.brass)),
          const SizedBox(height: AppSpacing.md),
          Text(
            formatDate(now).toUpperCase(),
            style: text.labelSmall!.copyWith(color: AppColors.muted, letterSpacing: 1.8),
          ),
          const SizedBox(height: AppSpacing.xs),
          Semantics(
            header: true,
            child: Text(
              '${StaffHomeScreen.greeting(now)}${name == null ? '' : ', $name'}',
              style: wide ? text.displaySmall : text.headlineLarge,
            ),
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            tasks == 0
                ? 'You are all caught up.'
                : (tasks == 1 ? '1 thing needs your attention.' : '$tasks things need your attention.'),
            style: text.bodyLarge!.copyWith(color: AppColors.muted),
          ),
        ],
      ),
    );
  }
}

/// Small square cover of a competition with a hairline round it.
class _CoverThumb extends StatelessWidget {
  const _CoverThumb({required this.competitionId, required this.hasCover, required this.label});

  final int competitionId;
  final bool hasCover;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 72,
      height: 72,
      decoration: BoxDecoration(border: Border.all(color: AppColors.line)),
      child: hasCover
          ? PublicImage(url: ApiUrls.competitionCover(competitionId), semanticLabel: 'Cover of $label')
          : const ImagePlaceholder(icon: Icons.palette_outlined),
    );
  }
}

/// One line of the to-do list: a hairline above, the content, and an arrow when it leads somewhere.
class _TodoRow extends StatelessWidget {
  const _TodoRow({required this.leading, required this.child, this.onTap});

  final Widget leading;
  final Widget child;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final row = DecoratedBox(
      decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.line))),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            leading,
            const SizedBox(width: AppSpacing.md),
            Expanded(child: child),
            if (onTap != null) const Icon(Icons.arrow_forward, size: 18, color: AppColors.brassText),
          ],
        ),
      ),
    );
    return onTap == null ? row : InkWell(onTap: onTap, child: row);
  }
}

class _ToGradeRow extends StatelessWidget {
  const _ToGradeRow({required this.item});

  final ToGradeItem item;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    // Information only: staff follow the progress, teachers grade (the entries open once the results are published)
    return Semantics(
      container: true,
      child: _TodoRow(
        leading: _CoverThumb(competitionId: item.competitionId, hasCover: item.hasCover, label: item.title),
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
    );
  }
}

class _ToPublishRow extends StatelessWidget {
  const _ToPublishRow({required this.item});

  final ToPublishItem item;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final empty = item.emptySlots;
    return _TodoRow(
      onTap: () => context.push('/staff/awarding/${item.competitionId}'),
      leading: _CoverThumb(competitionId: item.competitionId, hasCover: item.hasCover, label: item.title),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(item.title, style: text.titleMedium, maxLines: 2, overflow: TextOverflow.ellipsis),
          const SizedBox(height: 2),
          Text(
            empty == 0 ? 'All award slots are filled' : '$empty award ${empty == 1 ? 'slot' : 'slots'} still empty',
            style: text.bodySmall,
          ),
        ],
      ),
    );
  }
}

class _CountRow extends StatelessWidget {
  const _CountRow({required this.icon, required this.count, required this.title, required this.onTap});

  final IconData icon;
  final int count;
  final String title;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return _TodoRow(
      onTap: onTap,
      leading: Icon(icon, color: AppColors.brassText),
      child: Row(
        children: [
          // A large light numeral, like the prize ranks
          // A fixed box and a regular weight: a hairline "1" next to the icon reads as a stray bar
          SizedBox(
            width: 36,
            child: Text(
              '$count',
              textAlign: TextAlign.center,
              style: text.headlineMedium!.copyWith(color: AppColors.brassText, fontWeight: FontWeight.w500),
            ),
          ),
          const SizedBox(width: AppSpacing.sm),
          Expanded(child: Text(title, style: text.bodyLarge)),
        ],
      ),
    );
  }
}
