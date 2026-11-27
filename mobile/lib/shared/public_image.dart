import 'package:flutter/material.dart';

import '../app/motion.dart';
import '../app/tokens.dart';

/// Neutral placeholder block (stone) with an optional icon, used while loading, when
/// there is no image, and after an error.
class ImagePlaceholder extends StatelessWidget {
  const ImagePlaceholder({super.key, this.icon = Icons.image_outlined, this.label});

  final IconData icon;

  /// Short text under the icon (used by error states, e.g. "Image unavailable").
  final String? label;

  @override
  Widget build(BuildContext context) {
    // In a loading skeleton the placeholder is one grey block (its icon and label are not drawn)
    if (SkeletonScope.of(context)) return SkeletonBlock(child: _box(context));
    return _box(context);
  }

  Widget _box(BuildContext context) {
    return ColoredBox(
      color: AppColors.stone,
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, color: AppColors.muted, size: 28),
            if (label != null) ...[
              const SizedBox(height: 4),
              Text(label!, style: Theme.of(context).textTheme.bodySmall, textAlign: TextAlign.center),
            ],
          ],
        ),
      ),
    );
  }
}

/// Wraps a decoded image with a fade-in (skipped when the OS asks to reduce motion).
Widget fadeInFrame(BuildContext context, Widget child, int? frame, bool wasSynchronouslyLoaded) {
  if (wasSynchronouslyLoaded || MediaQuery.of(context).disableAnimations) return child;
  return AnimatedOpacity(
    opacity: frame == null ? 0 : 1,
    duration: AppMotion.fast,
    curve: AppMotion.enter,
    child: child,
  );
}

/// Image from a public URL (see `ApiUrls`): stone placeholder while loading, fade-in,
/// and an error state. Fills the space it is given; wrap it in `AspectRatio` / `SizedBox`
/// (or pass [aspectRatio]) to size it.
class PublicImage extends StatelessWidget {
  const PublicImage({
    super.key,
    required this.url,
    this.fit = BoxFit.cover,
    this.aspectRatio,
    this.semanticLabel,
    this.borderRadius,
    this.placeholderIcon = Icons.image_outlined,
  });

  /// Absolute image URL.
  final String url;
  final BoxFit fit;

  /// If set, the image is laid out in a box of this width / height ratio.
  final double? aspectRatio;

  /// Accessibility description (e.g. the artwork title). Null marks the image as decorative.
  final String? semanticLabel;
  final BorderRadius? borderRadius;
  final IconData placeholderIcon;

  @override
  Widget build(BuildContext context) {
    // A loading skeleton fetches nothing: the picture's box stays grey
    Widget image = SkeletonScope.of(context)
        ? ImagePlaceholder(icon: placeholderIcon)
        : Image.network(
            url,
            fit: fit,
            semanticLabel: semanticLabel,
            excludeFromSemantics: semanticLabel == null,
            frameBuilder: fadeInFrame,
            loadingBuilder: (context, child, progress) {
              if (progress == null) return child;
              return ImagePlaceholder(icon: placeholderIcon);
            },
            errorBuilder: (context, error, stack) =>
                const ImagePlaceholder(icon: Icons.broken_image_outlined, label: 'Image unavailable'),
          );
    if (borderRadius != null) image = ClipRRect(borderRadius: borderRadius!, child: image);
    if (aspectRatio != null) image = AspectRatio(aspectRatio: aspectRatio!, child: image);
    return image;
  }
}
