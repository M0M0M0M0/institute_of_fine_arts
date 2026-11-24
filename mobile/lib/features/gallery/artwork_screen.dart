import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/motion.dart';
import '../../app/tokens.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import 'gallery.dart';

/// M-G5: one public artwork, big and framed. Tap the picture to zoom.
class ArtworkScreen extends ConsumerWidget {
  const ArtworkScreen({super.key, required this.id});

  final int id;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final artwork = ref.watch(galleryArtworkProvider(id));
    return Scaffold(
      appBar: AppBar(leading: const AppBackButton(), leadingWidth: 48, titleSpacing: 0, title: const Text('Artwork')),
      body: AsyncValueView(
        value: artwork,
        onRetry: () => ref.invalidate(galleryArtworkProvider(id)),
        data: (a) => _ArtworkBody(artwork: a),
      ),
    );
  }
}

class _ArtworkBody extends StatelessWidget {
  const _ArtworkBody({required this.artwork});

  final GalleryArtwork artwork;

  void _zoom(BuildContext context) => ArtworkViewer.open(
        context,
        ArtworkViewer.public(
          submissionId: artwork.submissionId,
          title: artwork.title,
          subtitle: artwork.studentName,
        ),
      );

  /// The framed painting, tappable to zoom. The frame hugs the painting at its own proportions.
  Widget _picture(BuildContext context) {
    final a = artwork;
    return Semantics(
      button: true,
      label: 'Zoom in on ${a.title}',
      child: InkWell(
        onTap: () => _zoom(context),
        child: ArtworkFrame(
          awarded: a.isAwarded,
          awardRank: a.awardRank,
          aspectRatio: null,
          matWidth: 16,
          child: Stack(
            children: [
              // Same tag as the tile in the gallery grid, so the picture flies in from there
              ArtworkHero(
                submissionId: a.submissionId,
                child: NaturalArtworkImage.public(submissionId: a.submissionId, semanticLabel: a.title),
              ),
              const Positioned(
                right: AppSpacing.sm,
                bottom: AppSpacing.sm,
                child: CircleAvatar(
                  radius: 18,
                  backgroundColor: Colors.black54,
                  child: Icon(Icons.zoom_in, color: Colors.white, size: 22),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  /// The wall label: award, title, artist, date, and the competition the work belongs to.
  Widget _label(BuildContext context, {required bool wide}) {
    final a = artwork;
    final text = Theme.of(context).textTheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (a.isAwarded) ...[
          StatusChip.custom(
            label: a.awardName!,
            color: AppColors.medalText(a.awardRank),
            icon: Icons.emoji_events_outlined,
          ),
          const SizedBox(height: AppSpacing.md),
        ],
        const SizedBox(width: 28, height: 2, child: ColoredBox(color: AppColors.brass)),
        const SizedBox(height: AppSpacing.md),
        Semantics(
          header: true,
          child: Text(
            a.title,
            style: (wide ? text.displaySmall : text.headlineMedium)!.copyWith(fontStyle: FontStyle.italic),
          ),
        ),
        const SizedBox(height: AppSpacing.sm),
        Text(
          a.studentName.toUpperCase(),
          style: text.labelLarge!.copyWith(letterSpacing: 1.8, color: AppColors.muted),
        ),
        if (a.submittedAt != null) ...[
          const SizedBox(height: AppSpacing.xs),
          Text('Submitted ${formatDate(a.submittedAt!)}', style: text.bodySmall),
        ],
        const SizedBox(height: AppSpacing.xl),
        const Divider(),
        const SizedBox(height: AppSpacing.md),
        Text(
          'FROM THE COMPETITION',
          style: text.labelSmall!.copyWith(color: AppColors.muted, letterSpacing: 1.8),
        ),
        const SizedBox(height: AppSpacing.xs),
        InkWell(
          onTap: a.competitionId == 0 ? null : () => context.push('/competitions/${a.competitionId}'),
          child: ConstrainedBox(
            constraints: const BoxConstraints(minHeight: AppSpacing.touch),
            child: Row(
              children: [
                Expanded(child: Text(a.competitionTitle, style: text.titleMedium)),
                if (a.competitionId != 0) const Icon(Icons.arrow_forward, size: 18, color: AppColors.brassText),
              ],
            ),
          ),
        ),
        if ((a.competitionDescription ?? '').isNotEmpty) ...[
          const SizedBox(height: AppSpacing.xs),
          Text(a.competitionDescription!, style: text.bodyMedium!.copyWith(color: AppColors.muted)),
        ],
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final width = MediaQuery.sizeOf(context).width;
    final wide = width >= AppLayout.wide;
    final gutter = AppLayout.gutterFor(width);

    // Wide: the painting on the left and its label beside it, like a work on a gallery wall.
    // Phone: the painting, then the label under it.
    return SingleChildScrollView(
      padding: EdgeInsets.fromLTRB(gutter, wide ? AppSpacing.xl : AppSpacing.md, gutter, AppSpacing.xxl),
      child: Center(
        child: ConstrainedBox(
          constraints: BoxConstraints(maxWidth: wide ? 1040 : 560),
          child: wide
              ? Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(flex: 6, child: _picture(context)),
                    const SizedBox(width: AppSpacing.xxl),
                    Expanded(flex: 4, child: _label(context, wide: true)),
                  ],
                )
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _picture(context),
                    const SizedBox(height: AppSpacing.xl),
                    _label(context, wide: false),
                  ],
                ),
        ),
      ),
    );
  }
}
