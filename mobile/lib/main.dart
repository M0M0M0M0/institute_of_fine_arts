import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app/router.dart';
import 'core/push.dart';
import 'features/auth/auth.dart';
import 'features/notifications/notifications.dart';
import 'app/splash_screen.dart';
import 'app/theme.dart';
import 'app/wide_canvas.dart';

/// Điểm khởi chạy app. ProviderScope bọc toàn bộ để dùng Riverpod.
void main() {
  runApp(const ProviderScope(child: FineArtsApp()));
}

/// Root widget: go_router navigation by role, and push notifications (core/push.dart).
class FineArtsApp extends ConsumerStatefulWidget {
  const FineArtsApp({super.key});

  @override
  ConsumerState<FineArtsApp> createState() => _FineArtsAppState();
}

class _FineArtsAppState extends ConsumerState<FineArtsApp> {
  /// Shows a push that arrives while the app is open.
  final _messenger = GlobalKey<ScaffoldMessengerState>();

  @override
  void initState() {
    super.initState();
    ref.read(pushProvider).start(onMessage: _showPush, onOpen: _openPush);
  }

  void _showPush(String title, String body, String? link) {
    _messenger.currentState?.showSnackBar(SnackBar(
      content: Text(body.isEmpty ? title : '$title: $body', maxLines: 3, overflow: TextOverflow.ellipsis),
      action: SnackBarAction(label: 'Open', onPressed: () => _openPush(link)),
    ));
  }

  /// A tapped push opens its screen, or the notification list when the app has no such screen.
  void _openPush(String? link) => ref.read(routerProvider).push(appRouteFor(link) ?? '/notifications');

  @override
  Widget build(BuildContext context) {
    final router = ref.watch(routerProvider);
    // Signed in (login or restored session) → register this phone for push
    ref.listen(authControllerProvider, (prev, next) {
      final before = prev == null ? null : currentUserOf(prev)?.accountId;
      final now = currentUserOf(next)?.accountId;
      if (now != null && now != before) ref.read(pushProvider).signedIn();
    });
    return MaterialApp.router(
      title: 'IFA',
      debugShowCheckedModeBanner: false,
      theme: buildTheme(),
      scaffoldMessengerKey: _messenger,
      routerConfig: router,
      // Animated splash over the first page (after the native splash), then fades away
      builder: (context, child) => WideCanvas(child: ArtSplashScreen(child: child ?? const SizedBox.shrink())),
    );
  }
}
