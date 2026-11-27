import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';

import 'tokens.dart';

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

/// A tappable area that answers the finger at once: it shrinks a little on touch-down (not on release), springs back
/// on release or when the touch turns into a scroll, keeps the Material ripple and gives a light haptic tick on tap.
/// Use it for cards, artwork tiles and list rows instead of a bare [InkWell]; standard buttons already have their own
/// feedback. With reduced motion the shrink is skipped (the ripple and the haptic stay).
class Pressable extends StatefulWidget {
  const Pressable({
    super.key,
    required this.child,
    required this.onTap,
    this.onHover,
    this.pressedScale = 0.97,
    this.haptic = true,
    this.borderRadius,
  });

  final Widget child;
  final VoidCallback? onTap;
  final ValueChanged<bool>? onHover;

  /// Scale while pressed: 0.96 to 0.98 (below 0.95 looks jumpy). Full-width rows use 0.98.
  final double pressedScale;
  final bool haptic;
  final BorderRadius? borderRadius;

  @override
  State<Pressable> createState() => _PressableState();
}

class _PressableState extends State<Pressable> {
  bool _pressed = false;

  void _set(bool value) {
    if (_pressed != value) setState(() => _pressed = value);
  }

  @override
  Widget build(BuildContext context) {
    final reduce = MediaQuery.disableAnimationsOf(context);
    final onTap = widget.onTap;
    return InkWell(
      onTap: onTap == null
          ? null
          : () {
              _set(false);
              if (widget.haptic) HapticFeedback.selectionClick();
              onTap();
            },
      onTapDown: onTap == null ? null : (_) => _set(true),
      onTapUp: (_) => _set(false),
      onTapCancel: () => _set(false),
      onHover: widget.onHover,
      hoverColor: widget.onHover == null ? null : Colors.transparent,
      borderRadius: widget.borderRadius,
      child: AnimatedScale(
        scale: _pressed && !reduce ? widget.pressedScale : 1,
        duration: reduce ? Duration.zero : AppMotion.press,
        curve: AppMotion.curve,
        child: widget.child,
      ),
    );
  }
}

/// Lets the first items of a list appear one after the other (fade and a short rise, [AppMotion.stagger] apart), so
/// the eye follows the list as it arrives. Only the first [maxStaggered] items wait; the rest, and every item built
/// later while scrolling, show at once. Nothing moves with reduced motion. Each item plays once.
class StaggerIn extends StatefulWidget {
  const StaggerIn({super.key, required this.index, required this.child, this.maxStaggered = 6});

  final int index;
  final Widget child;
  final int maxStaggered;

  @override
  State<StaggerIn> createState() => _StaggerInState();
}

class _StaggerInState extends State<StaggerIn> with SingleTickerProviderStateMixin {
  AnimationController? _controller;
  Animation<double>? _fade;
  Animation<Offset>? _rise;
  bool _decided = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_decided) return;
    _decided = true;
    if (widget.index >= widget.maxStaggered || MediaQuery.disableAnimationsOf(context) || SkeletonScope.of(context)) {
      return;
    }
    // One controller covers the item's delay and its own appearance, so there is no timer to cancel
    final delay = AppMotion.stagger * widget.index;
    final total = delay + AppMotion.appear;
    final controller = AnimationController(vsync: this, duration: total);
    final curve = CurvedAnimation(
      parent: controller,
      curve: Interval(delay.inMicroseconds / total.inMicroseconds, 1, curve: AppMotion.enter),
    );
    _controller = controller;
    _fade = curve;
    _rise = Tween<Offset>(begin: const Offset(0, 0.04), end: Offset.zero).animate(curve);
    controller.forward();
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final fade = _fade;
    if (fade == null) return widget.child;
    return FadeTransition(
      opacity: fade,
      child: SlideTransition(position: _rise!, child: widget.child),
    );
  }
}

/// Grey blocks in the shape of the content that is loading (cards, rows), softly pulsing; used instead of a spinner
/// where the shape of the content is known. The pulse loops, so it is an explicit animation; with reduced motion the
/// blocks stay still.
class SkeletonPulse extends StatefulWidget {
  const SkeletonPulse({super.key, required this.child});

  final Widget child;

  @override
  State<SkeletonPulse> createState() => _SkeletonPulseState();
}

class _SkeletonPulseState extends State<SkeletonPulse> with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(vsync: this, duration: Durations.extralong4);
  late final Animation<double> _opacity = Tween<double>(
    begin: 0.45,
    end: 1,
  ).animate(CurvedAnimation(parent: _controller, curve: AppMotion.curve));

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (MediaQuery.disableAnimationsOf(context)) {
      _controller.stop();
      _controller.value = 1;
    } else if (!_controller.isAnimating) {
      _controller.repeat(reverse: true);
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Semantics(
    label: 'Loading',
    child: FadeTransition(
      opacity: _opacity,
      child: ExcludeSemantics(child: widget.child),
    ),
  );
}

/// Marks the screen below as a loading skeleton: widgets that would fetch something (pictures) show a grey block
/// instead, list items do not play their entrance.
class SkeletonScope extends InheritedWidget {
  const SkeletonScope({super.key, required super.child});

  /// Whether [context] is inside a skeleton.
  static bool of(BuildContext context) => context.dependOnInheritedWidgetOfExactType<SkeletonScope>() != null;

  @override
  bool updateShouldNotify(SkeletonScope oldWidget) => false;
}

/// Loading skeleton built from the real layout: [child] is the screen's own widget filled with placeholder data;
/// it is laid out normally but not painted. Instead every line of text becomes a grey bar exactly where (and as
/// wide as) that line is, and every picture a grey block of its size, all softly pulsing. When the data arrives the
/// real content takes the very same place, so nothing jumps. Not tappable, hidden from screen readers (which hear
/// "Loading").
class Skeleton extends StatelessWidget {
  const Skeleton({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) => SkeletonPulse(
    child: IgnorePointer(
      child: SkeletonScope(child: _Skeletonize(child: child)),
    ),
  );
}

/// A grey block in a [Skeleton] in place of [child] (a picture); what is inside is not drawn.
class SkeletonBlock extends SingleChildRenderObjectWidget {
  const SkeletonBlock({super.key, super.child});

  @override
  RenderObject createRenderObject(BuildContext context) => _RenderSkeletonBlock();
}

class _RenderSkeletonBlock extends RenderProxyBox {}

class _Skeletonize extends SingleChildRenderObjectWidget {
  const _Skeletonize({required super.child});

  @override
  RenderObject createRenderObject(BuildContext context) => _RenderSkeletonize();
}

/// Paints, instead of its child, a bar on every laid-out line of text and a block on every [SkeletonBlock].
class _RenderSkeletonize extends RenderProxyBox {
  static final _paint = Paint()..color = AppColors.stone;

  @override
  void paint(PaintingContext context, Offset offset) {
    final root = child;
    if (root == null) return;
    final canvas = context.canvas
      ..save()
      ..clipRect(offset & size);
    void visit(RenderObject node) {
      if (node is RenderOffstage && node.offstage) return;
      if (node is RenderBox && node.hasSize) {
        if (node is _RenderSkeletonBlock) {
          final rect = MatrixUtils.transformRect(node.getTransformTo(this), Offset.zero & node.size);
          canvas.drawRect(rect.shift(offset), _paint);
          return;
        }
        if (node is RenderParagraph) {
          final length = node.text.toPlainText(includeSemanticsLabels: false).length;
          final transform = node.getTransformTo(this);
          for (final box in node.getBoxesForSelection(TextSelection(baseOffset: 0, extentOffset: length))) {
            final line = MatrixUtils.transformRect(transform, box.toRect()).shift(offset);
            // A bar about as tall as the letters, centred on the line, so lines stay apart
            final bar = line;
            final inset = line.height * 0.2;
            canvas.drawRRect(
              RRect.fromRectAndRadius(
                Rect.fromLTRB(bar.left, bar.top + inset, bar.right, bar.bottom - inset),
                const Radius.circular(2),
              ),
              _paint,
            );
          }
          return;
        }
      }
      node.visitChildren(visit);
    }

    visit(root);
    canvas.restore();
  }
}

/// Hand-over from one state of a screen to the next (skeleton → content, content → error), as a Material "fade
/// through": the old state fades out quickly first ([AppMotion.handOverOut]); the new one waits until the old one is
/// mostly gone, then fades in and rises a few pixels, settling slowly ([AppMotion.handOverIn]). The two never sit on
/// top of each other at half opacity, which read as a jump. Give each state its own key. Instant with reduced motion.
class StateSwitcher extends StatelessWidget {
  const StateSwitcher({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return AnimatedSwitcher(
      duration: motionDuration(context, AppMotion.handOverIn),
      reverseDuration: motionDuration(context, AppMotion.handOverOut),
      // The first 30 % of the entrance waits for the old state to fade, the rest decelerates into place
      switchInCurve: const Interval(0.3, 1, curve: AppMotion.enter),
      switchOutCurve: AppMotion.curve,
      transitionBuilder: (child, animation) {
        final fade = FadeTransition(opacity: animation, child: child);
        // The skeleton only fades; real content also rises a little as it arrives
        if (child.key == const ValueKey('loading')) return fade;
        return SlideTransition(
          position: Tween<Offset>(begin: const Offset(0, 0.02), end: Offset.zero).animate(animation),
          child: fade,
        );
      },
      // Top-aligned, so a list does not jump while the old state fades out; the parent's constraints pass through
      // unchanged, so each state lays out exactly as it did without the hand-over
      layoutBuilder: (current, previous) =>
          Stack(alignment: Alignment.topCenter, fit: StackFit.passthrough, children: [...previous, ?current]),
      child: child,
    );
  }
}
