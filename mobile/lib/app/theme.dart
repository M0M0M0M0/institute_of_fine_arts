import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show SystemUiOverlayStyle;

import 'tokens.dart';

/// App theme: web palette (navy ink, stone, brass) and General Sans for every text style (headings use a heavier weight).
/// The colour scheme is built from explicit hex values.
ThemeData buildTheme() {
  const scheme = ColorScheme(
    brightness: Brightness.light,
    primary: AppColors.ink,
    onPrimary: Colors.white,
    primaryContainer: AppColors.mist,
    onPrimaryContainer: AppColors.ink,
    secondary: AppColors.brassText,
    onSecondary: Colors.white,
    secondaryContainer: AppColors.stone,
    onSecondaryContainer: AppColors.graphite,
    tertiary: AppColors.brass,
    onTertiary: Colors.white,
    tertiaryContainer: Color(0xFFF1E7D6),
    onTertiaryContainer: AppColors.brassText,
    error: AppColors.danger,
    onError: Colors.white,
    errorContainer: Color(0xFFF6DEDA),
    onErrorContainer: AppColors.danger,
    surface: AppColors.wall,
    onSurface: AppColors.graphite,
    onSurfaceVariant: AppColors.muted,
    outline: AppColors.muted,
    outlineVariant: AppColors.line,
    shadow: Colors.black,
    scrim: Colors.black,
    inverseSurface: AppColors.ink,
    onInverseSurface: Colors.white,
    inversePrimary: AppColors.stone,
    surfaceTint: Colors.transparent,
    surfaceContainerLowest: AppColors.wall,
    surfaceContainerLow: Color(0xFFF7F6F2),
    surfaceContainer: Color(0xFFF2F0EA),
    surfaceContainerHigh: AppColors.mist,
    surfaceContainerHighest: AppColors.stone,
  );

  final text = _textTheme();
  final shape = RoundedRectangleBorder(borderRadius: AppRadius.smAll);
  const buttonPadding = EdgeInsets.symmetric(horizontal: 20, vertical: 12);
  const minSize = Size(64, AppSpacing.touch);

  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: AppColors.wall,
    canvasColor: AppColors.wall,
    fontFamily: AppFonts.body,
    textTheme: text,
    primaryTextTheme: text,
    dividerColor: AppColors.line,
    dividerTheme: const DividerThemeData(color: AppColors.line, thickness: 1, space: 1),
    // Light bar with a hairline, like a museum wall label: the artworks carry the colour, not the chrome
    appBarTheme: AppBarTheme(
      backgroundColor: AppColors.wall,
      foregroundColor: AppColors.ink,
      surfaceTintColor: Colors.transparent,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: false,
      shape: const Border(bottom: BorderSide(color: AppColors.line)),
      systemOverlayStyle: SystemUiOverlayStyle.dark,
      titleTextStyle: text.titleLarge!.copyWith(color: AppColors.ink, letterSpacing: 0.6),
      iconTheme: const IconThemeData(color: AppColors.ink),
      actionsIconTheme: const IconThemeData(color: AppColors.ink),
    ),
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: AppColors.wall,
      surfaceTintColor: Colors.transparent,
      indicatorColor: Colors.transparent,
      height: 68,
      elevation: 0,
      labelTextStyle: WidgetStateProperty.resolveWith(
        (states) => TextStyle(
          fontFamily: AppFonts.body,
          // 11 with tight tracking (about 67 dp): the longest label ("Competitions") fits a 5-tab bar on a 360 dp phone
          fontSize: 11,
          letterSpacing: -0.2,
          fontWeight: states.contains(WidgetState.selected) ? FontWeight.w600 : FontWeight.w500,
          color: states.contains(WidgetState.selected) ? AppColors.ink : AppColors.muted,
        ),
      ),
      iconTheme: WidgetStateProperty.resolveWith(
        (states) => IconThemeData(
          size: 24,
          color: states.contains(WidgetState.selected) ? AppColors.brassText : AppColors.muted,
        ),
      ),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: AppColors.ink,
        foregroundColor: Colors.white,
        minimumSize: minSize,
        padding: buttonPadding,
        shape: shape,
        textStyle: text.labelLarge,
      ),
    ),
    elevatedButtonTheme: ElevatedButtonThemeData(
      style: ElevatedButton.styleFrom(
        backgroundColor: AppColors.ink,
        foregroundColor: Colors.white,
        minimumSize: minSize,
        padding: buttonPadding,
        shape: shape,
        elevation: 0,
        textStyle: text.labelLarge,
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: AppColors.ink,
        minimumSize: minSize,
        padding: buttonPadding,
        shape: shape,
        side: const BorderSide(color: AppColors.ink),
        textStyle: text.labelLarge,
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: AppColors.ink,
        minimumSize: const Size(AppSpacing.touch, AppSpacing.touch),
        shape: shape,
        textStyle: text.labelLarge,
      ),
    ),
    iconButtonTheme: IconButtonThemeData(
      style: IconButton.styleFrom(minimumSize: const Size(AppSpacing.touch, AppSpacing.touch)),
    ),
    chipTheme: ChipThemeData(
      backgroundColor: AppColors.wall,
      selectedColor: AppColors.ink,
      disabledColor: AppColors.stone,
      side: const BorderSide(color: AppColors.line),
      shape: shape,
      labelStyle: text.labelMedium!.copyWith(color: AppColors.graphite),
      secondaryLabelStyle: text.labelMedium!.copyWith(color: Colors.white),
      checkmarkColor: Colors.white,
      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 4),
    ),
    cardTheme: CardThemeData(
      color: AppColors.wall,
      surfaceTintColor: Colors.transparent,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: AppRadius.mdAll,
        side: const BorderSide(color: AppColors.line),
      ),
    ),
    listTileTheme: const ListTileThemeData(iconColor: AppColors.ink, minVerticalPadding: 8),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: AppColors.wall,
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
      border: OutlineInputBorder(
        borderRadius: AppRadius.smAll,
        borderSide: const BorderSide(color: AppColors.muted),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: AppRadius.smAll,
        borderSide: const BorderSide(color: AppColors.muted),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: AppRadius.smAll,
        borderSide: const BorderSide(color: AppColors.ink, width: 2),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: AppRadius.smAll,
        borderSide: const BorderSide(color: AppColors.danger),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: AppRadius.smAll,
        borderSide: const BorderSide(color: AppColors.danger, width: 2),
      ),
      labelStyle: text.bodyMedium!.copyWith(color: AppColors.muted),
      floatingLabelStyle: text.bodyMedium!.copyWith(color: AppColors.ink),
      hintStyle: text.bodyMedium!.copyWith(color: AppColors.muted),
      errorStyle: text.bodySmall!.copyWith(color: AppColors.danger),
    ),
    snackBarTheme: SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      backgroundColor: AppColors.ink,
      contentTextStyle: text.bodyMedium!.copyWith(color: Colors.white),
      actionTextColor: AppColors.stone,
      shape: shape,
    ),
    progressIndicatorTheme: const ProgressIndicatorThemeData(
      color: AppColors.ink,
      linearTrackColor: AppColors.stone,
      circularTrackColor: AppColors.stone,
    ),
    dialogTheme: DialogThemeData(
      backgroundColor: AppColors.wall,
      surfaceTintColor: Colors.transparent,
      elevation: 12,
      shadowColor: const Color(0x33000000),
      shape: const RoundedRectangleBorder(
        borderRadius: AppRadius.mdAll,
        side: BorderSide(color: AppColors.line),
      ),
      titleTextStyle: text.headlineSmall,
      contentTextStyle: text.bodyMedium,
    ),
    bottomSheetTheme: const BottomSheetThemeData(
      backgroundColor: AppColors.wall,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadius.md))),
    ),
    segmentedButtonTheme: SegmentedButtonThemeData(
      style: SegmentedButton.styleFrom(
        selectedBackgroundColor: AppColors.ink,
        selectedForegroundColor: Colors.white,
        foregroundColor: AppColors.ink,
        side: const BorderSide(color: AppColors.ink),
        shape: shape,
        minimumSize: const Size(0, AppSpacing.touch),
      ),
    ),
    tabBarTheme: TabBarThemeData(
      labelColor: AppColors.ink,
      unselectedLabelColor: AppColors.muted,
      indicatorColor: AppColors.brass,
      dividerColor: AppColors.line,
      labelStyle: text.labelLarge,
      unselectedLabelStyle: text.labelLarge,
    ),
  );
}

/// General Sans everywhere (same as the web's menus): titles use a heavier weight, body / label a regular one.
/// Bodoni Moda titles were dropped because they were hard to read on a phone.
TextTheme _textTheme() {
  TextStyle title(double size, FontWeight weight, {double? height}) => TextStyle(
    fontFamily: AppFonts.display,
    fontSize: size,
    fontWeight: weight,
    height: height,
    color: AppColors.graphite,
  );
  TextStyle sans(double size, FontWeight weight, {double? height, double letter = 0, Color? color}) => TextStyle(
    fontFamily: AppFonts.body,
    fontSize: size,
    fontWeight: weight,
    height: height,
    letterSpacing: letter,
    color: color ?? AppColors.graphite,
  );

  // Large titles are light with tight tracking (editorial); small titles stay medium so they read well on a phone
  return TextTheme(
    displayLarge: title(52, FontWeight.w300, height: 1.05).copyWith(letterSpacing: -1.2),
    displayMedium: title(42, FontWeight.w300, height: 1.1).copyWith(letterSpacing: -0.9),
    displaySmall: title(34, FontWeight.w300, height: 1.15).copyWith(letterSpacing: -0.6),
    headlineLarge: title(30, FontWeight.w300, height: 1.2).copyWith(letterSpacing: -0.4),
    headlineMedium: title(26, FontWeight.w400, height: 1.25).copyWith(letterSpacing: -0.3),
    headlineSmall: title(22, FontWeight.w400, height: 1.25).copyWith(letterSpacing: -0.2),
    titleLarge: title(20, FontWeight.w500, height: 1.3),
    titleMedium: title(17, FontWeight.w500, height: 1.3),
    titleSmall: title(15, FontWeight.w500, height: 1.3),
    bodyLarge: sans(16, FontWeight.w400, height: 1.6),
    bodyMedium: sans(14, FontWeight.w400, height: 1.55),
    bodySmall: sans(14, FontWeight.w400, height: 1.45, color: AppColors.muted),
    labelLarge: sans(14, FontWeight.w600, height: 1.2, letter: 0.8),
    labelMedium: sans(14, FontWeight.w500, height: 1.2, letter: 0.2),
    labelSmall: sans(12, FontWeight.w600, height: 1.2, letter: 1.0),
  );
}
