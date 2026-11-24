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
/// winners (when published) and one action that depends on the state and the role.
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
          appBar: AppBar(leading: const AppBackButton(), leadingWidth: 48, titleSpacing: 0, title: const Text('Competition')),
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
    final width = MediaQuery.sizeOf(context).width;
    final wide = width >= AppLayout.wide;
    final gutter = AppLayout.gutterFor(width);
    final countdown = s.countdownLabel;
    final action = _actionButton(context);

    // Text blocks (overview, rules) and the practical side (dates, prizes)
    final story = <Widget>[
      if ((d.description ?? '').isNotEmpty) _TextBlock(title: 'Overview', body: d.description!),
      if ((d.conditions ?? '').isNotEmpty) _TextBlock(title: 'Rules', body: d.conditions!),
      if (d.timeline.isNotEmpty) _Timeline(items: d.timeline),
    ];
    final practical = <Widget>[
      _DatesPanel(detail: d),
      // Wide screens keep the action next to the facts; phones get it in a bar at the bottom
      if (wide && action != null) ...[
        const SizedBox(height: AppSpacing.lg),
        SizedBox(width: double.infinity, child: action),
      ],
      if (d.awards.isNotEmpty) ...[
        const SizedBox(height: AppSpacing.xl),
        const _Eyebrow('Prizes'),
        for (final a in d.awards) _PrizeRow(award: a),
      ],
    ];

    return Scaffold(
      appBar: AppBar(leading: const AppBackButton(), leadingWidth: 48, titleSpacing: 0, title: const Text('Competition')),
      bottomNavigationBar: wide ? null : _actionBar(action),
      body: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            HeroHeader(
              // Status and time left as the small line above the title
              eyebrow: [competitionStatusLabel(s.status), ?countdown].join('  ·  '),
              title: s.title,
              subtitle: (s.topic ?? '').isEmpty ? null : s.topic,
              background: s.hasCover
                  ? PublicImage(url: ApiUrls.competitionCover(s.id), semanticLabel: 'Cover of ${s.title}')
                  : null,
              height: 280,
            ),
            Padding(
              padding: EdgeInsets.fromLTRB(gutter, wide ? AppSpacing.xxl : AppSpacing.lg, gutter, 0),
              child: wide
                  ? Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(flex: 6, child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: story)),
                        const SizedBox(width: AppSpacing.xxl),
                        Expanded(flex: 4, child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: practical)),
                      ],
                    )
                  // Phone: the facts first, then the story, then the prizes
                  : Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        practical.first,
                        const SizedBox(height: AppSpacing.xl),
                        ...story,
                        ...practical.skip(1),
                      ],
                    ),
            ),
            if (s.status == CompetitionStatus.graded) ...[
              KeyedSubtree(
                key: _winnersKey,
                child: const SectionHeader('Winners'),
              ),
              _Winners(competitionId: s.id),
            ],
            const SizedBox(height: AppSpacing.xxl),
          ],
        ),
      ),
    );
  }

  // ---- action by state and role ----

  Widget? _actionBar(Widget? button) {
    if (button == null) return null;
    return DecoratedBox(
      decoration: const BoxDecoration(
        color: AppColors.wall,
        border: Border(top: BorderSide(color: AppColors.line)),
      ),
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

/// Small caps heading with a short brass rule above it: the label of a block of facts.
class _Eyebrow extends StatelessWidget {
  const _Eyebrow(this.label);

  final String label;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SizedBox(width: 28, height: 2, child: ColoredBox(color: AppColors.brass)),
          const SizedBox(height: AppSpacing.sm),
          Semantics(
            header: true,
            child: Text(
              label.toUpperCase(),
              style: text.labelSmall!.copyWith(fontSize: 12.5, letterSpacing: 1.8, color: AppColors.ink),
            ),
          ),
        ],
      ),
    );
  }
}

/// A heading and a paragraph of the competition text (overview, rules).
class _TextBlock extends StatelessWidget {
  const _TextBlock({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _Eyebrow(title),
          // Written with the web's rich text editor (BR-30): sanitized HTML; older competitions have plain text
          RichTextView(body, style: Theme.of(context).textTheme.bodyLarge),
        ],
      ),
    );
  }
}

/// Timeline (step 7): a line of dots, the current one filled and marked "Now", past ones muted.
class _Timeline extends StatelessWidget {
  const _Timeline({required this.items});

  final List<TimelineItem> items;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _Eyebrow('Timeline'),
          for (var i = 0; i < items.length; i++)
            IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // Dot and the line to the next one
                  SizedBox(
                    width: 24,
                    child: Column(
                      children: [
                        const SizedBox(height: 4),
                        _Dot(state: items[i].state),
                        if (i < items.length - 1) Expanded(child: Container(width: 2, color: AppColors.line)),
                      ],
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Padding(
                      padding: EdgeInsets.only(bottom: i < items.length - 1 ? AppSpacing.md : 0),
                      child: Semantics(
                        label: items[i].state == TimelineState.current ? 'Now' : null,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(formatDate(items[i].date), style: text.bodySmall!.copyWith(color: AppColors.muted)),
                            Row(
                              children: [
                                Flexible(
                                  child: Text(
                                    items[i].title,
                                    style: text.titleSmall!.copyWith(
                                      color: items[i].state == TimelineState.past ? AppColors.muted : AppColors.ink,
                                    ),
                                  ),
                                ),
                                if (items[i].state == TimelineState.current) ...[
                                  const SizedBox(width: AppSpacing.xs),
                                  Text('NOW', style: text.labelSmall!.copyWith(color: AppColors.ink, letterSpacing: 1.2)),
                                ],
                              ],
                            ),
                            if ((items[i].content ?? '').isNotEmpty)
                              Padding(
                                padding: const EdgeInsets.only(top: 2),
                                child: Text(items[i].content!, style: text.bodyMedium),
                              ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _Dot extends StatelessWidget {
  const _Dot({required this.state});

  final TimelineState state;

  @override
  Widget build(BuildContext context) {
    final filled = state != TimelineState.upcoming;
    final color = state == TimelineState.current ? AppColors.ink : AppColors.muted;
    return Container(
      width: 14,
      height: 14,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: filled ? color : AppColors.wall,
        border: Border.all(color: color, width: 2),
      ),
    );
  }
}

/// Dates and entries as a wall label: small caps name, value, hairline between the rows.
class _DatesPanel extends StatelessWidget {
  const _DatesPanel({required this.detail});

  final CompetitionDetail detail;

  @override
  Widget build(BuildContext context) {
    final s = detail.summary;
    final text = Theme.of(context).textTheme;
    final rows = [
      ('Runs', '${formatDate(s.startDate)} – ${formatDate(s.endDate)}'),
      ('Deadline', formatDateTime(detail.deadline)),
      if (s.status == CompetitionStatus.ongoing || s.status == CompetitionStatus.closed)
        ('Entries', '${s.submissionCount}'),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const _Eyebrow('Details'),
        for (var i = 0; i < rows.length; i++)
          DecoratedBox(
            decoration: BoxDecoration(
              border: Border(
                top: const BorderSide(color: AppColors.line),
                bottom: i == rows.length - 1 ? const BorderSide(color: AppColors.line) : BorderSide.none,
              ),
            ),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.baseline,
                textBaseline: TextBaseline.alphabetic,
                children: [
                  SizedBox(
                    width: 96,
                    child: Text(
                      rows[i].$1.toUpperCase(),
                      style: text.labelSmall!.copyWith(color: AppColors.muted, letterSpacing: 1.4),
                    ),
                  ),
                  Expanded(child: Text(rows[i].$2, style: text.bodyLarge!.copyWith(fontWeight: FontWeight.w500))),
                ],
              ),
            ),
          ),
      ],
    );
  }
}

/// One prize: a large brass numeral, the name, the value and a short description.
class _PrizeRow extends StatelessWidget {
  const _PrizeRow({required this.award});

  final CompetitionAward award;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return DecoratedBox(
      decoration: const BoxDecoration(border: Border(top: BorderSide(color: AppColors.line))),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              width: 44,
              child: Text(
                '${award.rank}',
                style: text.headlineMedium!.copyWith(color: AppColors.brass, fontWeight: FontWeight.w300),
              ),
            ),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(award.name, style: text.titleMedium),
                  Text(
                    '${formatMoney(award.prizeValue)}  ·  ${award.quantity} ${award.quantity == 1 ? 'award' : 'awards'}',
                    style: text.bodyMedium!.copyWith(color: AppColors.brassText, fontWeight: FontWeight.w500),
                  ),
                  if ((award.description ?? '').isNotEmpty)
                    Padding(
                      padding: const EdgeInsets.only(top: AppSpacing.xs),
                      child: Text(award.description!, style: text.bodyMedium!.copyWith(color: AppColors.muted)),
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
    final gutter = AppLayout.gutterFor(MediaQuery.sizeOf(context).width);
    return switch (awards) {
      // The grid brings its own side gutter, so it sits outside the padded content
      AsyncData(:final value) => value.isEmpty
          ? Padding(
              padding: EdgeInsets.symmetric(horizontal: gutter),
              child: const Text('Results have not been published yet.'),
            )
          : TwoColumnGrid(tiles: [
              for (final a in value)
                if (a.submissionId != null)
                  ArtworkTile.public(
                    submissionId: a.submissionId!,
                    title: a.paintingTitle,
                    artist: a.studentName,
                    awardName: a.awardName,
                    awardRank: a.rank,
                    awarded: true,
                    onTap: () => context.push('/gallery/artworks/${a.submissionId}'),
                  )
                else
                  _TextWinner(award: a),
            ]),
      AsyncError(:final error) => Padding(
          padding: EdgeInsets.symmetric(horizontal: gutter, vertical: AppSpacing.sm),
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
      WallLabel(
        title: award.paintingTitle,
        artist: award.studentName,
        awardName: award.awardName,
        awardRank: award.rank,
      );
}
