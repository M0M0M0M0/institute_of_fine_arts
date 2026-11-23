import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app/router.dart';
import 'app/splash_screen.dart';
import 'app/theme.dart';
import 'app/wide_canvas.dart';

/// Điểm khởi chạy app. ProviderScope bọc toàn bộ để dùng Riverpod.
void main() {
  runApp(const ProviderScope(child: FineArtsApp()));
}

/// Widget gốc: dùng go_router để điều hướng theo vai trò người dùng.
class FineArtsApp extends ConsumerWidget {
  const FineArtsApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    return MaterialApp.router(
      title: 'IFA',
      debugShowCheckedModeBanner: false,
      theme: buildTheme(),
      routerConfig: router,
      // Animated splash over the first page (after the native splash), then fades away
      builder: (context, child) => WideCanvas(child: ArtSplashScreen(child: child ?? const SizedBox.shrink())),
    );
  }
}
