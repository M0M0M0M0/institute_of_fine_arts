import 'package:flutter/material.dart';

/// Colour tokens shared with the web app (see docs/plan/05 section 3).
abstract final class AppColors {
  /// Gallery wall.
  static const wall = Color(0xFFFFFFFF);

  /// Stone: the passe-partout mat around artworks.
  static const stone = Color(0xFFE6E3D9);

  /// Mist: soft blue-grey panels.
  static const mist = Color(0xFFEAF0F3);

  /// Navy ink: primary colour, navigation bars.
  static const ink = Color(0xFF0B2A33);

  /// Body text.
  static const graphite = Color(0xFF2B2924);

  /// Secondary text.
  static const muted = Color(0xFF625D53);

  /// Hairlines and dividers.
  static const line = Color(0xFFD6D2C8);

  /// Brass accent: gilt frame of awarded works.
  static const brass = Color(0xFFA8814A);

  /// Brass for small text (passes 4.5:1 on white).
  static const brassText = Color(0xFF7A5B2E);

  /// Errors and destructive actions.
  static const danger = Color(0xFFA23B2A);

  // Status colours.
  static const statusUpcoming = Color(0xFF2C5D8A);
  static const statusOngoing = Color(0xFF2F6B4F);
  static const statusClosed = Color(0xFF625D53);
  static const statusPublished = ink;
}

/// Spacing scale (logical pixels).
abstract final class AppSpacing {
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 16;
  static const double lg = 24;
  static const double xl = 32;
  static const double xxl = 48;

  /// Default horizontal screen gutter.
  static const double gutter = 16;

  /// Minimum touch target.
  static const double touch = 44;
}

/// Corner radii: small, like the web.
abstract final class AppRadius {
  static const double sm = 4;
  static const double md = 8;
  static const BorderRadius smAll = BorderRadius.all(Radius.circular(sm));
  static const BorderRadius mdAll = BorderRadius.all(Radius.circular(md));
}

/// Font family names registered in pubspec.yaml.
abstract final class AppFonts {
  /// Titles: same family as the body (Bodoni Moda was hard to read), heavier weight set in the theme
  static const display = 'GeneralSans';
  static const body = 'GeneralSans';
}

/// Motion tokens: how long and how fast things move. Use through `motionDuration` (app/motion.dart),
/// which returns zero when the OS asks to reduce motion.
abstract final class AppMotion {
  /// Small effects (zoom, fades inside a screen).
  static const fast = Duration(milliseconds: 200);

  /// Screen transitions (artwork opens from the gallery).
  static const med = Duration(milliseconds: 320);

  /// Starts quick, settles softly.
  static const curve = Curves.easeOutCubic;
}
