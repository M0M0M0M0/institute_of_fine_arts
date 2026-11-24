import 'dart:ui' show ImageFilter;

import 'package:flutter/material.dart';

import '../app/motion.dart';
import '../app/tokens.dart';
import 'api_urls.dart';
import 'auth_image.dart';
import 'public_image.dart';

/// Full-screen artwork viewer: pinch / drag to zoom, double-tap to zoom in and out,
/// close button, optional caption (title + subtitle) at the bottom.
///
/// Behind the picture sits the picture itself, enlarged, blurred and darkened with navy ink, like a gallery wall lit by
/// the work. Set [blurBackground] to false for a flat dimmed copy (cheaper on slow devices).
///
/// Show a public artwork with [ArtworkViewer.public] or a private one (own entry, staff)
/// with [ArtworkViewer.private]; open it with [ArtworkViewer.open].
class ArtworkViewer extends StatefulWidget {
  /// Public artwork shown from `/files/submissions/{id}` (full size). The backdrop uses the thumbnail, which the
  /// grid has already loaded and which is plenty for a blurred picture.
  ArtworkViewer.public({super.key, required int submissionId, this.title, this.subtitle, this.blurBackground = true})
      : image = PublicImage(url: ApiUrls.submissionFull(submissionId), fit: BoxFit.contain, semanticLabel: title),
        backdrop = PublicImage(url: ApiUrls.submissionThumb(submissionId));

  /// Private artwork loaded through the API with the JWT (full size).
  ArtworkViewer.private({super.key, required int submissionId, this.title, this.subtitle, this.blurBackground = true})
      : image = AuthImage(submissionId: submissionId, thumb: false, fit: BoxFit.contain, semanticLabel: title),
        backdrop = AuthImage(submissionId: submissionId);

  /// Any image widget that fills its box (use `BoxFit.contain`). Pass [backdrop] (a decorative copy that fills its box
  /// with `BoxFit.cover`) to get the blurred background; without it the background is plain navy ink.
  const ArtworkViewer({
    super.key,
    required this.image,
    this.title,
    this.subtitle,
    this.backdrop,
    this.blurBackground = true,
  });

  final Widget image;
  final String? title;
  final String? subtitle;

  /// Decorative copy of the picture for the background (no semantic label), or null for a plain background.
  final Widget? backdrop;

  /// Blur the background picture (sigma 6). False shows it sharp under a darker veil.
  final bool blurBackground;

  /// Pushes the viewer as a full-screen route.
  static Future<void> open(BuildContext context, ArtworkViewer viewer) =>
      Navigator.of(context).push(MaterialPageRoute<void>(fullscreenDialog: true, builder: (_) => viewer));

  @override
  State<ArtworkViewer> createState() => _ArtworkViewerState();
}

class _ArtworkViewerState extends State<ArtworkViewer> with SingleTickerProviderStateMixin {
  static const _zoomedScale = 2.5;

  final _controller = TransformationController();
  late final AnimationController _anim = AnimationController(vsync: this, duration: AppMotion.fast);
  Offset _tapPosition = Offset.zero;

  /// The picture again, enlarged 1.25x, optionally blurred, under a veil of navy ink.
  Widget _background() {
    final picture = widget.backdrop;
    if (picture == null) return const ColoredBox(color: AppColors.ink);
    final blur = widget.blurBackground;
    return ExcludeSemantics(
      child: IgnorePointer(
        child: Stack(
          fit: StackFit.expand,
          children: [
            ClipRect(
              child: Transform.scale(
                scale: 1.25,
                child: blur
                    ? ImageFiltered(
                        imageFilter: ImageFilter.blur(sigmaX: 6, sigmaY: 6),
                        child: SizedBox.expand(child: picture),
                      )
                    : SizedBox.expand(child: picture),
              ),
            ),
            // A sharp copy needs a darker veil than a blurred one to keep the foreground picture readable
            ColoredBox(color: AppColors.ink.withValues(alpha: blur ? 0.7 : 0.85)),
          ],
        ),
      ),
    );
  }

  @override
  void dispose() {
    _anim.dispose();
    _controller.dispose();
    super.dispose();
  }

  void _toggleZoom() {
    final zoomedIn = _controller.value.getMaxScaleOnAxis() > 1.05;
    final Matrix4 target;
    if (zoomedIn) {
      target = Matrix4.identity();
    } else {
      // Zoom around the tapped point.
      target = Matrix4.identity()
        ..translateByDouble(-_tapPosition.dx * (_zoomedScale - 1), -_tapPosition.dy * (_zoomedScale - 1), 0, 1)
        ..scaleByDouble(_zoomedScale, _zoomedScale, 1, 1);
    }
    final duration = motionDuration(context, AppMotion.fast);
    if (duration == Duration.zero) {
      _controller.value = target;
      return;
    }
    _anim.duration = duration;
    final animation = Matrix4Tween(begin: _controller.value, end: target)
        .animate(CurvedAnimation(parent: _anim, curve: AppMotion.curve));
    void listener() => _controller.value = animation.value;
    animation.addListener(listener);
    _anim.forward(from: 0).whenComplete(() => animation.removeListener(listener));
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final hasCaption = (widget.title ?? '').isNotEmpty || (widget.subtitle ?? '').isNotEmpty;
    return Scaffold(
      backgroundColor: AppColors.ink,
      body: Stack(
        fit: StackFit.expand,
        children: [
          _background(),
          GestureDetector(
            onDoubleTapDown: (d) => _tapPosition = d.localPosition,
            onDoubleTap: _toggleZoom,
            child: InteractiveViewer(
              transformationController: _controller,
              minScale: 1,
              maxScale: 6,
              child: SizedBox.expand(child: widget.image),
            ),
          ),
          SafeArea(
            child: Align(
              alignment: Alignment.topRight,
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.sm),
                child: IconButton.filled(
                  tooltip: 'Close',
                  style: IconButton.styleFrom(backgroundColor: Colors.black54, foregroundColor: Colors.white),
                  icon: const Icon(Icons.close),
                  onPressed: () => Navigator.of(context).maybePop(),
                ),
              ),
            ),
          ),
          if (hasCaption)
            Align(
              alignment: Alignment.bottomCenter,
              child: IgnorePointer(
                child: Container(
                  width: double.infinity,
                  padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.xl, AppSpacing.md, AppSpacing.md),
                  decoration: const BoxDecoration(
                    gradient: LinearGradient(
                      begin: Alignment.topCenter,
                      end: Alignment.bottomCenter,
                      colors: [Colors.transparent, Colors.black87],
                    ),
                  ),
                  child: SafeArea(
                    top: false,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        if ((widget.title ?? '').isNotEmpty)
                          Text(
                            widget.title!,
                            style: text.titleMedium!.copyWith(color: Colors.white, fontStyle: FontStyle.italic),
                          ),
                        if ((widget.subtitle ?? '').isNotEmpty)
                          Text(widget.subtitle!, style: text.bodyMedium!.copyWith(color: Colors.white70)),
                      ],
                    ),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}
