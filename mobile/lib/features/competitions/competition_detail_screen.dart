import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import '../auth/auth.dart';
import '../gallery/gallery_widgets.dart';
import '../submissions/submissions.dart';
import 'competitions.dart';

/// M-G3: competition detail (shared by guest and student). Cover, dates, overview, rules, prizes,
/// winners (when published) and one action at the bottom that depends on the state and the role.
class CompetitionDetailScreen extends ConsumerWidget {
  const CompetitionDetailScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(competitionDetailProvider(id));
    final user = currentUserOf(ref.watch(authControllerProvider));

    return switch (detail) {
      AsyncData(:final value) => _DetailView(detail: value, user: user),
      _ => Scaffold(
          appBar: AppBar(title: const Text('Competition')),
          body: AsyncValueView(
            value: detail,
            onRetry: () => ref.invalidate(competitionDetailProvider(id)),
            data: (_) => const SizedBox.shrink(),
          ),
        ),
    };
  }
}

class _DetailView extends ConsumerStatefulWidget {
  const _DetailView({required this.detail, required this.user});

  final CompetitionDetail detail;
  final AppUser? user;

  @override
  ConsumerState<_DetailView> createState() => _DetailViewState();
}

class _DetailViewState extends ConsumerState<_DetailView> {
  final _winnersKey = GlobalKey();

  CompetitionSummary get _s => widget.detail.summary;

  void _scrollToWinners() {
    final ctx = _winnersKey.currentContext;
    if (ctx != null) Scrollable.ensureVisible(ctx, duration: const Duration(milliseconds: 300));
  }

  @override
  Widget build(BuildContext context) {
    final d = widget.detail;
    final s = d.summary;
    final text = Theme.of(context).textTheme;
    final countdown = s.countdownLabel;

    return Scaffold(
      bottomNavigationBar: _actionBar(context),
      body: CustomScrollView(
        slivers: [
          SliverAppBar(
            pinned: true,
            expandedHeight: 240,
            flexibleSpace: FlexibleSpaceBar(
              background: Stack(
                fit: StackFit.expand,
                children: [
                  if (s.hasCover)
                    PublicImage(url: ApiUrls.competitionCover(s.id), semanticLabel: 'Cover of ${s.title}')
                  else
                    const ColoredBox(color: AppColors.stone, child: ImagePlaceholder(icon: Icons.palette_outlined)),
                  const DecoratedBox(
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        begin: Alignment.topCenter,
                        end: Alignment.bottomCenter,
                        colors: [Color(0x990B2A33), Color(0x00000000)],
                        stops: [0, 0.5],
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          SliverPadding(
            padding: const EdgeInsets.all(AppSpacing.gutter),
            sliver: SliverList.list(children: [
              Wrap(
                spacing: AppSpacing.sm,
                runSpacing: AppSpacing.xs,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  StatusChip(s.status),
                  if (countdown != null)
                    Text(countdown,
                        style: text.labelLarge!.copyWith(
                            color: s.status == CompetitionStatus.ongoing ? AppColors.brassText : AppColors.muted)),
                ],
              ),
              const SizedBox(height: AppSpacing.sm),
              Semantics(header: true, child: Text(s.title, style: text.headlineMedium)),
              if (s.topic != null && s.topic!.isNotEmpty) ...[
                const SizedBox(height: AppSpacing.xs),
                Text(s.topic!, style: text.titleMedium!.copyWith(color: AppColors.muted)),
              ],
              const SizedBox(height: AppSpacing.md),
              _DatesPanel(detail: d),
              if ((d.description ?? '').isNotEmpty) ...[
                _heading('Overview'),
                Text(d.description!, style: text.bodyLarge),
              ],
              if ((d.conditions ?? '').isNotEmpty) ...[
                _heading('Rules'),
                Text(d.conditions!, style: text.bodyLarge),
              ],
              if (d.awards.isNotEmpty) ...[
                _heading('Prizes'),
                for (final a in d.awards) _PrizeCard(award: a),
              ],
              if (s.status == CompetitionStatus.graded) ...[
                KeyedSubtree(key: _winnersKey, child: _heading('Winners')),
                _Winners(competitionId: s.id),
              ],
              const SizedBox(height: AppSpacing.lg),
            ]),
          ),
        ],
      ),
    );
  }

  Widget _heading(String title) => Padding(
        padding: const EdgeInsets.only(top: AppSpacing.lg, bottom: AppSpacing.sm),
        child: Semantics(header: true, child: Text(title, style: Theme.of(context).textTheme.headlineSmall)),
      );

  // ---- bottom action by state and role ----

  Widget? _actionBar(BuildContext context) {
    final button = _actionButton(context);
    if (button == null) return null;
    return Material(
      color: AppColors.wall,
      elevation: 8,
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: SizedBox(width: double.infinity, child: button),
        ),
      ),
    );
  }

  Widget? _actionButton(BuildContext context) {
    final user = widget.user;
    switch (_s.status) {
      case CompetitionStatus.ongoing:
        if (user == null) {
          return FilledButton(onPressed: () => context.go('/login'), child: const Text('Sign in to enter'));
        }
        if (user.role != Role.student) return null;
        final mine = ref.watch(mySubmissionIdProvider(_s.id));
        final existing = mine.value;
        if (existing != null) {
          return FilledButton(
            onPressed: () => context.push('/student/submissions/$existing'),
            child: const Text('View my entry'),
          );
        }
        return FilledButton(
          // Wait until we know whether the student has already entered
          onPressed: mine.isLoading ? null : () => context.push('/student/submissions/new?competitionId=${_s.id}'),
          child: const Text('Submit your entry'),
        );
      case CompetitionStatus.upcoming:
        return FilledButton(onPressed: null, child: Text('Opens on ${formatDate(_s.startDate)}'));
      case CompetitionStatus.closed:
        return const FilledButton(onPressed: null, child: Text('Submissions closed'));
      case CompetitionStatus.graded:
        return FilledButton(onPressed: _scrollToWinners, child: const Text('View all artworks'));
    }
  }
}

class _DatesPanel extends StatelessWidget {
  const _DatesPanel({required this.detail});

  final CompetitionDetail detail;

  @override
  Widget build(BuildContext context) {
    final s = detail.summary;
    final text = Theme.of(context).textTheme;
    Widget row(IconData icon, String label, String value) => Padding(
          padding: const EdgeInsets.symmetric(vertical: 2),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(icon, size: 18, color: AppColors.muted),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text.rich(TextSpan(children: [
                  TextSpan(text: '$label  ', style: text.bodyMedium!.copyWith(color: AppColors.muted)),
                  TextSpan(text: value, style: text.bodyMedium!.copyWith(fontWeight: FontWeight.w600)),
                ])),
              ),
            ],
          ),
        );

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(color: AppColors.mist, borderRadius: AppRadius.mdAll),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          row(Icons.event_outlined, 'Runs', '${formatDate(s.startDate)} – ${formatDate(s.endDate)}'),
          row(Icons.schedule, 'Deadline', formatDateTime(detail.deadline)),
          if (s.status == CompetitionStatus.ongoing || s.status == CompetitionStatus.closed)
            row(Icons.image_outlined, 'Entries', '${s.submissionCount}'),
        ],
      ),
    );
  }
}

class _PrizeCard extends StatelessWidget {
  const _PrizeCard({required this.award});

  final CompetitionAward award;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            CircleAvatar(
              backgroundColor: AppColors.brass,
              foregroundColor: Colors.white,
              child: Text('${award.rank}', style: const TextStyle(fontWeight: FontWeight.w700)),
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(award.name, style: text.titleMedium),
                  Text(
                    '${formatMoney(award.prizeValue)}  ·  ${award.quantity} ${award.quantity == 1 ? 'award' : 'awards'}',
                    style: text.bodyMedium!.copyWith(color: AppColors.brassText, fontWeight: FontWeight.w600),
                  ),
                  if ((award.description ?? '').isNotEmpty)
                    Padding(
                      padding: const EdgeInsets.only(top: AppSpacing.xs),
                      child: Text(award.description!, style: text.bodyMedium),
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

class _Winners extends ConsumerWidget {
  const _Winners({required this.competitionId});

  final int competitionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final awards = ref.watch(competitionAwardsProvider(competitionId));
    return switch (awards) {
      AsyncData(:final value) => value.isEmpty
          ? const Text('Results have not been published yet.')
          : TwoColumnGrid(tiles: [
              for (final a in value)
                if (a.submissionId != null)
                  ArtworkTile.public(
                    submissionId: a.submissionId!,
                    title: a.paintingTitle,
                    artist: a.studentName,
                    awardName: a.awardName,
                    awarded: true,
                    onTap: () => context.push('/gallery/artworks/${a.submissionId}'),
                  )
                else
                  _TextWinner(award: a),
            ]),
      AsyncError(:final error) => Padding(
          padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
          child: Text('Could not load the winners. $error'),
        ),
      _ => const Padding(padding: EdgeInsets.all(AppSpacing.md), child: Center(child: CircularProgressIndicator())),
    };
  }
}

/// A winner without a public picture: just the wall label.
class _TextWinner extends StatelessWidget {
  const _TextWinner({required this.award});

  final AwardResult award;

  @override
  Widget build(BuildContext context) =>
      WallLabel(title: award.paintingTitle, artist: award.studentName, awardName: award.awardName);
}
