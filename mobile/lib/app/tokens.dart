import 'package:flutter/material.dart';

/// Colour tokens shared with the web app (see docs/plan/05 section 3).
abstract final class AppColors {
  /// Gallery wall: warm off-white, so the stone mats and the artworks stand out.
  static const wall = Color(0xFFFBFAF7);

  /// The floor around the page on wide screens.
  static const floor = Color(0xFFEFECE4);

  /// Stone: the passe-partout mat around artworks.
  static const stone = Color(0xFFE6E3D9);

  /// Mist: soft panels (warm paper, to sit with the warm wall; it used to be blue-grey).
  static const mist = Color(0xFFF3F0E8);

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

  // Medal colours: the frame of a prize-winning work follows its rank.
  static const gold = Color(0xFFC9A03C);
  static const silver = Color(0xFF9BA5AB);
  static const bronze = Color(0xFFA8643A);

  /// Frame colour for a prize rank: 1 gold, 2 silver, 3 bronze, any other prize the brass accent.
  static Color medalFor(int? rank) => switch (rank) {
    1 => gold,
    2 => silver,
    3 => bronze,
    _ => brass,
  };

  /// Same hues darkened for small text on the wall (passes 4.5:1).
  static Color medalText(int? rank) => switch (rank) {
    1 => const Color(0xFF7A5B12),
    2 => const Color(0xFF586166),
    3 => const Color(0xFF86492A),
    _ => brassText,
  };

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

  /// Minimum touch target (48 x 48, Flutter accessibility guideline and Material on Android).
  static const double touch = 48;
}

/// Layout on wide screens (web / tablet): the page is a centred column, like a catalogue page.
abstract final class AppLayout {
  /// Widest the content gets.
  static const double maxWidth = 1120;

  /// From this width on, navigation moves to a top bar and grids get more columns.
  static const double wide = 840;

  /// Side gutter: roomier on wide screens.
  static double gutterFor(double width) => width >= 700 ? 40 : AppSpacing.gutter;

  /// Columns of an artwork grid.
  static int columnsFor(double width) => width >= 900 ? 4 : (width >= 600 ? 3 : 2);
}

/// Corner radii: square corners, like frames and wall labels.
abstract final class AppRadius {
  static const double sm = 0;
  static const double md = 2;
  static const BorderRadius smAll = BorderRadius.all(Radius.circular(sm));
  static const BorderRadius mdAll = BorderRadius.all(Radius.circular(md));
}

/// Font family names registered in pubspec.yaml.
abstract final class AppFonts {
  /// Titles: same family as the body (Bodoni Moda was hard to read), heavier weight set in the theme
  static const display = 'GeneralSans';
  static const body = 'GeneralSans';
}

/// Motion tokens, taken from Flutter's Material 3 `Durations` and `Easing` instead of made-up numbers
/// (docs: UX guidelines, section 3). Use the lengths through `motionDuration` (app/motion.dart), which returns zero
/// when the OS asks to reduce motion. The smaller and more frequent the effect, the shorter it is; leaving is a little
/// shorter than entering.
abstract final class AppMotion {
  /// Press feedback (a card shrinking under the finger): Durations.short2.
  static const press = Durations.short2;

  /// Small state changes and fades inside a screen (zoom, an image fading in, a tab underline): Durations.short4.
  static const fast = Durations.short4;

  /// Elements appearing (list items, skeleton turning into content): Durations.medium1.
  static const appear = Durations.medium1;

  /// Screen transitions (artwork opens from the gallery): Durations.medium2.
  static const med = Durations.medium2;

  /// Leaving a screen, a little shorter than entering it: Durations.medium1.
  static const exit = Durations.medium1;

  /// A screen's content arriving in place of its loading skeleton (a large element: Durations.medium4).
  static const handOverIn = Durations.medium4;

  /// The skeleton fading away before the content arrives (Durations.short4).
  static const handOverOut = Durations.short4;

  /// Delay between list items that appear one after the other (30 to 50 ms; only the first few are staggered).
  static const stagger = Duration(milliseconds: 40);

  /// Moving or changing inside the screen: Easing.standard.
  static const curve = Easing.standard;

  /// Entering the screen (fast, then settling): Easing.emphasizedDecelerate.
  static const enter = Easing.emphasizedDecelerate;

  /// Leaving the screen (slow, then fast): Easing.emphasizedAccelerate.
  static const leave = Easing.emphasizedAccelerate;
}
