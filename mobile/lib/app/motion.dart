import 'package:flutter/material.dart';

/// [duration], or zero when the OS asks to reduce motion ("Remove animations" / "Reduce motion").
/// Pass every animation length through this, e.g. `motionDuration(context, AppMotion.med)`.
Duration motionDuration(BuildContext context, Duration duration) =>
    MediaQuery.disableAnimationsOf(context) ? Duration.zero : duration;

/// Hero tag shared by the picture in the gallery grid and the one on the artwork screen.
/// A tag must be unique on a screen, so only give a picture a hero where that submission shows once.
String artworkHeroTag(int submissionId) => 'artwork-$submissionId';

/// A [Hero] for a public artwork picture: it flies from the grid to the artwork screen along a straight, light path
/// (a plain rectangle tween, no arc). The flight length follows the page transition, so it stops with reduced motion.
class ArtworkHero extends StatelessWidget {
  const ArtworkHero({super.key, required this.submissionId, required this.child});

  final int submissionId;
  final Widget child;

  @override
  Widget build(BuildContext context) => Hero(
        tag: artworkHeroTag(submissionId),
        createRectTween: (begin, end) => RectTween(begin: begin, end: end),
        child: child,
      );
}
