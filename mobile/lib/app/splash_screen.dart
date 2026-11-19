import 'package:flutter/material.dart';

import 'tokens.dart';

/// Animated splash shown over the app when it starts, right after the native splash (flutter_native_splash.yaml,
/// same #0D0D0D background and logo, so the hand-over is seamless).
///
/// The IFA logo lifts slightly while "INSTITUTE OF FINE ARTS" fades in and its letters spread out, like a gallery
/// wall label; after about 2.2 s the splash fades away and the app below (go_router) shows. It is an overlay instead
/// of a route because the app navigates with go_router: the router starts and redirects (signed in or not) underneath.
///
/// One AnimationController drives everything (no Timer), so widget tests that call pumpAndSettle simply wait for it.
class ArtSplashScreen extends StatefulWidget {
  const ArtSplashScreen({super.key, required this.child});

  /// The app (the router's current page).
  final Widget child;

  /// Background of the native and the animated splash.
  static const background = Color(0xFF0D0D0D);

  /// Gold of the logo.
  static const gold = Color(0xFFD6BE8A);

  /// Whole splash: 1.4 s intro, a short hold, then a 0.65 s fade-out ending at 2.85 s.
  static const total = Duration(milliseconds: 2850);

  @override
  State<ArtSplashScreen> createState() => _ArtSplashScreenState();
}

class _ArtSplashScreenState extends State<ArtSplashScreen> with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _logoLift;
  late final Animation<double> _subtitleFade;
  late final Animation<double> _letterSpacing;
  late final Animation<double> _splashFadeOut;
  bool _done = false;

  /// Interval of the whole splash between two moments in milliseconds.
  Interval _between(int fromMs, int toMs, Curve curve) {
    final total = ArtSplashScreen.total.inMilliseconds;
    return Interval(fromMs / total, toMs / total, curve: curve);
  }

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this, duration: ArtSplashScreen.total);

    // The IFA logo lifts a little while the subtitle appears
    _logoLift = Tween<double>(begin: 0, end: -12)
        .animate(CurvedAnimation(parent: _controller, curve: _between(140, 980, Curves.easeOutCubic)));
    // "INSTITUTE OF FINE ARTS" fades in…
    _subtitleFade = Tween<double>(begin: 0, end: 1)
        .animate(CurvedAnimation(parent: _controller, curve: _between(420, 1190, Curves.easeIn)));
    // …while its letters spread out, like an exhibition label
    _letterSpacing = Tween<double>(begin: 2, end: 5.5)
        .animate(CurvedAnimation(parent: _controller, curve: _between(420, 1260, Curves.easeOutCubic)));
    // At 2.2 s the splash fades away over the app
    _splashFadeOut = Tween<double>(begin: 1, end: 0)
        .animate(CurvedAnimation(parent: _controller, curve: _between(2200, 2850, Curves.easeOut)));

    _controller.addStatusListener((status) {
      if (status == AnimationStatus.completed && mounted) setState(() => _done = true);
    });
    _controller.forward();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (_done) return widget.child;
    return Stack(
      children: [
        widget.child,
        Positioned.fill(
          child: AnimatedBuilder(
            animation: _controller,
            builder: (context, _) => Opacity(
              opacity: _splashFadeOut.value,
              child: ColoredBox(
                color: ArtSplashScreen.background,
                child: Center(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Transform.translate(
                        offset: Offset(0, _logoLift.value),
                        child: Image.asset('assets/images/logo_ifa_transparent.png', width: 140, semanticLabel: 'IFA'),
                      ),
                      const SizedBox(height: 8),
                      Opacity(
                        opacity: _subtitleFade.value,
                        child: Text(
                          'INSTITUTE OF FINE ARTS',
                          style: TextStyle(
                            fontFamily: AppFonts.body,
                            color: ArtSplashScreen.gold,
                            fontSize: 12,
                            fontWeight: FontWeight.w400,
                            letterSpacing: _letterSpacing.value,
                            decoration: TextDecoration.none,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}
