import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app/router.dart';
import 'app/theme.dart';

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
      title: 'Institute of Fine Arts',
      debugShowCheckedModeBanner: false,
      theme: buildTheme(),
      routerConfig: router,
    );
  }
}
