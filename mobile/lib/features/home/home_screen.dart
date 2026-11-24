import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../shared/shared.dart';
import '../auth/auth.dart';
import '../competitions/competition_widgets.dart';
import '../competitions/competitions.dart';
import '../gallery/gallery.dart';
import '../gallery/gallery_widgets.dart';

/// M-G1: public home. Hero with the latest winning painting, open and upcoming competitions,
/// latest winners and a strip of the gallery.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final home = ref.watch(homeProvider);
    return Scaffold(
      body: AsyncValueView(
        value: home,
        onRetry: () => ref.invalidate(homeProvider),
        data: (data) => RefreshIndicator(
          edgeOffset: 40,
          onRefresh: () async {
            ref.invalidate(galleryPreviewProvider);
            ref.invalidate(homeProvider);
            await ref.read(homeProvider.future);
          },
          child: _HomeBody(data: data),
        ),
      ),
    );
  }
}

class _HomeBody extends ConsumerWidget {
  const _HomeBody({required this.data});

  final HomeData data;

  Widget? _heroBackground() {
    final winner = data.latestWinners.where((w) => w.submissionId != null).firstOrNull;
    if (winner != null) {
      return PublicImage(
        url: ApiUrls.submissionFull(winner.submissionId!),
        semanticLabel: 'Winning painting: ${winner.paintingTitle}',
      );
    }
    final withCover = [...data.ongoing, ...data.upcoming].where((c) => c.hasCover).firstOrNull;
    if (withCover != null) return PublicImage(url: ApiUrls.competitionCover(withCover.id));
    return null;
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final preview = ref.watch(galleryPreviewProvider).value;
    final winners = data.latestWinners.where((w) => w.submissionId != null).toList();
    // The gallery strip skips the paintings already shown under "Latest winners"
    final winnerIds = {for (final w in winners) w.submissionId};
    final galleryItems = preview?.items.where((a) => !winnerIds.contains(a.submissionId)).toList() ?? const [];
    final isEmpty =
        data.ongoing.isEmpty && data.upcoming.isEmpty && data.latestWinners.isEmpty && (preview?.items.isEmpty ?? true);

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: EdgeInsets.zero,
      children: [
        HeroHeader(
          eyebrow: 'Now showing',
          title: 'Institute of Fine Arts',
          subtitle: 'Where young painters compete, exhibit and are seen.',
          background: _heroBackground(),
          ctaLabel: 'Browse competitions',
          onCta: () => context.go('/competitions'),
          height: 360,
        ),
        if (isEmpty)
          const SizedBox(
            height: 320,
            child: EmptyState(title: 'Nothing on show yet', message: 'New competitions and artworks will appear here.'),
          ),
        if (data.ongoing.isNotEmpty) ...[
          SectionHeader(
            'Open now',
            subtitle: 'Competitions you can enter today',
            actionLabel: 'All',
            onAction: () => context.go('/competitions'),
          ),
          _competitionStrip(context, data.ongoing),
        ],
        if (data.upcoming.isNotEmpty) ...[
          const SectionHeader('Coming soon'),
          _competitionStrip(context, data.upcoming),
        ],
        if (winners.isNotEmpty) ...[
          SectionHeader('Latest winners', subtitle: data.latestGradedCompetition),
          TwoColumnGrid(tiles: [
            for (final w in winners)
              ArtworkTile.public(
                submissionId: w.submissionId!,
                title: w.paintingTitle,
                artist: w.studentName,
                awardName: w.awardName,
                awardRank: w.rank,
                awarded: true,
                onTap: () => context.push('/gallery/artworks/${w.submissionId}'),
              ),
          ]),
        ],
        if (galleryItems.isNotEmpty) ...[
          SectionHeader('From the gallery', actionLabel: 'See the gallery', onAction: () => context.go('/gallery')),
          SizedBox(
            height: MediaQuery.sizeOf(context).width >= AppLayout.wide ? 380 : 300,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              padding: EdgeInsets.symmetric(horizontal: AppLayout.gutterFor(MediaQuery.sizeOf(context).width)),
              itemCount: galleryItems.length,
              separatorBuilder: (context, i) => SizedBox(width: MediaQuery.sizeOf(context).width >= AppLayout.wide ? AppSpacing.lg : AppSpacing.md),
              itemBuilder: (context, i) {
                final a = galleryItems[i];
                return Align(
                  alignment: Alignment.topLeft,
                  child: SizedBox(
                    width: MediaQuery.sizeOf(context).width >= AppLayout.wide ? 220 : 160,
                    child: galleryTile(a,
                        showCompetition: false, onTap: () => context.push('/gallery/artworks/${a.submissionId}')),
                  ),
                );
              },
            ),
          ),
        ],
        const SizedBox(height: AppSpacing.xl),
      ],
    );
  }

  /// Competition cards: a horizontal strip on a phone (exactly as tall as the cards, no empty band below),
  /// a grid of columns on wide screens so the page does not end up with a few cards lost on the left.
  Widget _competitionStrip(BuildContext context, List<CompetitionSummary> items) {
    final width = MediaQuery.sizeOf(context).width;
    if (width >= AppLayout.wide) {
      return AdaptiveTileGrid(
        minTileWidth: 300,
        maxColumns: 3,
        children: [
          for (final c in items) CompetitionCard(competition: c, onTap: () => context.push('/competitions/${c.id}')),
        ],
      );
    }
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: EdgeInsets.symmetric(horizontal: AppLayout.gutterFor(width)),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (var i = 0; i < items.length; i++) ...[
            if (i > 0) const SizedBox(width: AppSpacing.md),
            CompetitionCard(
              competition: items[i],
              // A lone card fills the screen; with several, the next one peeks out to invite a swipe
              width: items.length == 1 ? width - 2 * AppLayout.gutterFor(width) : 280,
              onTap: () => context.push('/competitions/${items[i].id}'),
            ),
          ],
        ],
      ),
    );
  }
}

/// Đường dẫn trang chính theo vai trò. Admin/Manager dùng bản web (app chỉ phục vụ Student + Staff).
String homePathFor(Role role) => switch (role) {
      Role.student => '/student',
      Role.staff => '/staff',
      Role.teacher => '/teacher',
      _ => '/unsupported',
    };

/// Open and upcoming competitions as a plain list.
/// Still used by the student "Competitions" tab until the student agent switches it to `CompetitionsScreen`.
class CompetitionFeed extends ConsumerWidget {
  const CompetitionFeed({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final home = ref.watch(homeProvider);

    return AsyncValueView(
      value: home,
      onRetry: () => ref.invalidate(homeProvider),
      data: (data) => RefreshIndicator(
        onRefresh: () => ref.refresh(homeProvider.future),
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            const SectionTitle('Open now'),
            if (data.ongoing.isEmpty) const Text('No competition is open right now.'),
            for (final c in data.ongoing) _cardOf(context, c),
            const SectionTitle('Coming soon'),
            if (data.upcoming.isEmpty) const Text('No upcoming competition yet.'),
            for (final c in data.upcoming) _cardOf(context, c),
          ],
        ),
      ),
    );
  }

  Widget _cardOf(BuildContext context, CompetitionSummary c) => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: CompetitionCard(competition: c, onTap: () => context.push('/competitions/${c.id}')),
      );
}
