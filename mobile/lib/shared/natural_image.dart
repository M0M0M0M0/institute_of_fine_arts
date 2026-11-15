import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'api_urls.dart';
import 'auth_image.dart';
import 'public_image.dart';

/// A painting shown at its own proportions (portrait, landscape or square), as wide as the space allows and never
/// taller than [maxHeight]. Use it inside `ArtworkFrame(aspectRatio: null, …)` for the large picture of a detail
/// screen, so the frame hugs the painting instead of leaving empty bands around it.
class NaturalArtworkImage extends ConsumerWidget {
  /// Public painting (BEST or awarded, published): /files/submissions/{id}, no token needed.
  const NaturalArtworkImage.public({super.key, required this.submissionId, this.semanticLabel, this.maxHeight = 520})
      : isPublic = true;

  /// Private painting (own entry, or any entry for staff): /api/submissions/{id}/image with the JWT.
  const NaturalArtworkImage.private({super.key, required this.submissionId, this.semanticLabel, this.maxHeight = 520})
      : isPublic = false;

  final int submissionId;
  final bool isPublic;
  final String? semanticLabel;
  final double maxHeight;

  static const _boxHeight = 320.0;

  Widget _box(Widget child) => SizedBox(height: _boxHeight, width: double.infinity, child: child);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final Widget image;
    if (isPublic) {
      image = Image.network(
        ApiUrls.submissionFull(submissionId),
        width: double.infinity,
        fit: BoxFit.contain,
        semanticLabel: semanticLabel,
        excludeFromSemantics: semanticLabel == null,
        loadingBuilder: (context, child, progress) => progress == null ? child : _box(const ImagePlaceholder()),
        errorBuilder: (context, error, stack) =>
            _box(const ImagePlaceholder(icon: Icons.broken_image_outlined, label: 'Image unavailable')),
      );
    } else {
      final bytes = ref.watch(authImageProvider((submissionId, false)));
      image = switch (bytes) {
        AsyncData(:final value) => Image.memory(
            value,
            width: double.infinity,
            fit: BoxFit.contain,
            semanticLabel: semanticLabel,
            excludeFromSemantics: semanticLabel == null,
            gaplessPlayback: true,
            errorBuilder: (context, error, stack) =>
                _box(const ImagePlaceholder(icon: Icons.broken_image_outlined, label: 'Image unavailable')),
          ),
        AsyncError() => _box(InkWell(
            onTap: () => ref.invalidate(authImageProvider((submissionId, false))),
            child: const ImagePlaceholder(icon: Icons.refresh, label: 'Tap to retry'),
          )),
        _ => _box(const ImagePlaceholder()),
      };
    }
    // heightFactor 1: as tall as the painting (not the whole max height), centred horizontally when narrower
    return ConstrainedBox(
      constraints: BoxConstraints(maxHeight: maxHeight),
      child: Align(heightFactor: 1, child: image),
    );
  }
}
