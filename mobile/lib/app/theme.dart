import 'package:flutter/material.dart';

import 'tokens.dart';

/// App theme: web palette (navy ink, stone, brass), Bodoni Moda for headings,
/// General Sans for body text. The colour scheme is built from explicit hex values.
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
    appBarTheme: AppBarTheme(
      backgroundColor: AppColors.ink,
      foregroundColor: Colors.white,
      surfaceTintColor: Colors.transparent,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: false,
      titleTextStyle: text.titleLarge!.copyWith(color: Colors.white),
      iconTheme: const IconThemeData(color: Colors.white),
      actionsIconTheme: const IconThemeData(color: Colors.white),
    ),
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: AppColors.ink,
      surfaceTintColor: Colors.transparent,
      indicatorColor: AppColors.brassText,
      height: 68,
      elevation: 0,
      labelTextStyle: WidgetStateProperty.resolveWith((states) => TextStyle(
            fontFamily: AppFonts.body,
            fontSize: 12,
            fontWeight: states.contains(WidgetState.selected) ? FontWeight.w600 : FontWeight.w500,
            color: states.contains(WidgetState.selected) ? Colors.white : const Color(0xFFC9D3D6),
          )),
      iconTheme: WidgetStateProperty.resolveWith((states) => IconThemeData(
            size: 24,
            color: states.contains(WidgetState.selected) ? Colors.white : const Color(0xFFC9D3D6),
          )),
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
      border: OutlineInputBorder(borderRadius: AppRadius.smAll, borderSide: const BorderSide(color: AppColors.muted)),
      enabledBorder:
          OutlineInputBorder(borderRadius: AppRadius.smAll, borderSide: const BorderSide(color: AppColors.muted)),
      focusedBorder:
          OutlineInputBorder(borderRadius: AppRadius.smAll, borderSide: const BorderSide(color: AppColors.ink, width: 2)),
      errorBorder:
          OutlineInputBorder(borderRadius: AppRadius.smAll, borderSide: const BorderSide(color: AppColors.danger)),
      focusedErrorBorder:
          OutlineInputBorder(borderRadius: AppRadius.smAll, borderSide: const BorderSide(color: AppColors.danger, width: 2)),
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
      shape: RoundedRectangleBorder(borderRadius: AppRadius.mdAll),
      titleTextStyle: text.titleLarge,
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

/// Bodoni Moda for display / headline / title, General Sans for body / label.
/// Display sizes request the high optical size of the variable font.
TextTheme _textTheme() {
  TextStyle serif(double size, FontWeight weight, {double? height, double opsz = 28}) => TextStyle(
        fontFamily: AppFonts.display,
        fontSize: size,
        fontWeight: weight,
        height: height,
        color: AppColors.graphite,
        fontVariations: [FontVariation('opsz', opsz.clamp(6, 96).toDouble())],
      );
  TextStyle sans(double size, FontWeight weight, {double? height, double letter = 0, Color? color}) => TextStyle(
        fontFamily: AppFonts.body,
        fontSize: size,
        fontWeight: weight,
        height: height,
        letterSpacing: letter,
        color: color ?? AppColors.graphite,
      );

  return TextTheme(
    displayLarge: serif(44, FontWeight.w500, height: 1.1, opsz: 72),
    displayMedium: serif(36, FontWeight.w500, height: 1.15, opsz: 60),
    displaySmall: serif(30, FontWeight.w500, height: 1.2, opsz: 48),
    headlineLarge: serif(28, FontWeight.w500, height: 1.2, opsz: 40),
    headlineMedium: serif(24, FontWeight.w500, height: 1.25, opsz: 32),
    headlineSmall: serif(21, FontWeight.w500, height: 1.25, opsz: 28),
    titleLarge: serif(20, FontWeight.w600, height: 1.3, opsz: 24),
    titleMedium: serif(17, FontWeight.w600, height: 1.3, opsz: 18),
    titleSmall: serif(15, FontWeight.w600, height: 1.3, opsz: 14),
    bodyLarge: sans(16, FontWeight.w400, height: 1.5),
    bodyMedium: sans(14, FontWeight.w400, height: 1.5),
    bodySmall: sans(14, FontWeight.w400, height: 1.4, color: AppColors.muted),
    labelLarge: sans(15, FontWeight.w600, height: 1.2, letter: 0.1),
    labelMedium: sans(14, FontWeight.w500, height: 1.2, letter: 0.1),
    labelSmall: sans(12, FontWeight.w600, height: 1.2, letter: 0.4),
  );
}
