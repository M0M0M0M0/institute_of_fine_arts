import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

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
      appBar: AppBar(title: const Text('Artwork')),
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

  @override
  Widget build(BuildContext context) {
    final a = artwork;
    final text = Theme.of(context).textTheme;
    return SingleChildScrollView(
      padding: const EdgeInsets.all(AppSpacing.gutter),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 560),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Semantics(
                button: true,
                label: 'Zoom in on ${a.title}',
                child: InkWell(
                  onTap: () => _zoom(context),
                  // The frame hugs the painting at its own proportions (portrait or landscape)
                  child: ArtworkFrame(
                    awarded: a.isAwarded,
                    aspectRatio: null,
                    matWidth: 16,
                    child: Stack(
                      children: [
                        NaturalArtworkImage.public(submissionId: a.submissionId, semanticLabel: a.title),
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
              ),
              const SizedBox(height: AppSpacing.lg),
              WallLabel(
                title: a.title,
                artist: a.studentName,
                meta: a.submittedAt == null ? null : 'Submitted ${formatDate(a.submittedAt!)}',
                maxTitleLines: 4,
              ),
              if (a.isAwarded) ...[
                const SizedBox(height: AppSpacing.sm),
                Align(
                  alignment: Alignment.centerLeft,
                  child: StatusChip.custom(
                    label: a.awardName!,
                    color: AppColors.brassText,
                    icon: Icons.emoji_events_outlined,
                  ),
                ),
              ],
              const SizedBox(height: AppSpacing.lg),
              const Divider(),
              Text('From the competition', style: text.labelMedium),
              const SizedBox(height: AppSpacing.xs),
              InkWell(
                onTap: a.competitionId == 0 ? null : () => context.push('/competitions/${a.competitionId}'),
                child: ConstrainedBox(
                  constraints: const BoxConstraints(minHeight: AppSpacing.touch),
                  child: Row(
                    children: [
                      Expanded(child: Text(a.competitionTitle, style: text.titleMedium)),
                      const Icon(Icons.chevron_right),
                    ],
                  ),
                ),
              ),
              if ((a.competitionDescription ?? '').isNotEmpty)
                Text(a.competitionDescription!, style: text.bodyMedium!.copyWith(color: AppColors.muted)),
              const SizedBox(height: AppSpacing.lg),
            ],
          ),
        ),
      ),
    );
  }
}
