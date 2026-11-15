import 'package:flutter/material.dart';

import '../app/tokens.dart';
import 'api_urls.dart';
import 'auth_image.dart';
import 'public_image.dart';

/// Full-screen artwork viewer: pinch / drag to zoom, double-tap to zoom in and out,
/// close button, optional caption (title + subtitle) at the bottom.
///
/// Show a public artwork with [ArtworkViewer.public] or a private one (own entry, staff)
/// with [ArtworkViewer.private]; open it with [ArtworkViewer.open].
class ArtworkViewer extends StatefulWidget {
  /// Public artwork shown from `/files/submissions/{id}` (full size).
  ArtworkViewer.public({super.key, required int submissionId, this.title, this.subtitle})
      : image = PublicImage(url: ApiUrls.submissionFull(submissionId), fit: BoxFit.contain, semanticLabel: title);

  /// Private artwork loaded through the API with the JWT (full size).
  ArtworkViewer.private({super.key, required int submissionId, this.title, this.subtitle})
      : image = AuthImage(submissionId: submissionId, thumb: false, fit: BoxFit.contain, semanticLabel: title);

  /// Any image widget that fills its box (use `BoxFit.contain`).
  const ArtworkViewer({super.key, required this.image, this.title, this.subtitle});

  final Widget image;
  final String? title;
  final String? subtitle;

  /// Pushes the viewer as a full-screen route.
  static Future<void> open(BuildContext context, ArtworkViewer viewer) =>
      Navigator.of(context).push(MaterialPageRoute<void>(fullscreenDialog: true, builder: (_) => viewer));

  @override
  State<ArtworkViewer> createState() => _ArtworkViewerState();
}

class _ArtworkViewerState extends State<ArtworkViewer> with SingleTickerProviderStateMixin {
  static const _zoomedScale = 2.5;

  final _controller = TransformationController();
  late final AnimationController _anim = AnimationController(vsync: this, duration: const Duration(milliseconds: 200));
  Offset _tapPosition = Offset.zero;

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
    final animation = Matrix4Tween(begin: _controller.value, end: target)
        .animate(CurvedAnimation(parent: _anim, curve: Curves.easeOut));
    void listener() => _controller.value = animation.value;
    animation.addListener(listener);
    final reduce = MediaQuery.of(context).disableAnimations;
    if (reduce) {
      _controller.value = target;
      return;
    }
    _anim.forward(from: 0).whenComplete(() => animation.removeListener(listener));
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final hasCaption = (widget.title ?? '').isNotEmpty || (widget.subtitle ?? '').isNotEmpty;
    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(
        fit: StackFit.expand,
        children: [
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
