import 'package:flutter/material.dart';

/// Fades the right edge of a sideways scrolling row while there is more to see beyond it, so a half cut word reads
/// as "keep scrolling" and not as a mistake. The fade goes away once the end is reached or when everything fits.
class ScrollEdgeFade extends StatefulWidget {
  const ScrollEdgeFade({super.key, required this.child, this.width = 36});

  final Widget child;
  final double width;

  @override
  State<ScrollEdgeFade> createState() => _ScrollEdgeFadeState();
}

class _ScrollEdgeFadeState extends State<ScrollEdgeFade> {
  bool _moreToRight = false;

  bool _update(ScrollMetrics m) {
    final more = m.axis == Axis.horizontal && m.maxScrollExtent - m.pixels > 4;
    if (more != _moreToRight) setState(() => _moreToRight = more);
    return false;
  }

  @override
  Widget build(BuildContext context) {
    return NotificationListener<ScrollMetricsNotification>(
      onNotification: (n) => _update(n.metrics),
      child: NotificationListener<ScrollNotification>(
        onNotification: (n) => _update(n.metrics),
        child: LayoutBuilder(
          builder: (context, constraints) {
            if (!_moreToRight || !constraints.maxWidth.isFinite) {
              return widget.child;
            }
            final stop = (1 - widget.width / constraints.maxWidth).clamp(
              0.0,
              1.0,
            );
            return ShaderMask(
              blendMode: BlendMode.dstIn,
              shaderCallback: (rect) => LinearGradient(
                colors: const [Colors.white, Colors.white, Color(0x00FFFFFF)],
                stops: [0, stop, 1],
              ).createShader(rect),
              child: widget.child,
            );
          },
        ),
      ),
    );
  }
}
