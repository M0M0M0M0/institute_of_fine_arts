import 'package:fine_arts_mobile/app/theme.dart';
import 'package:fine_arts_mobile/app/tokens.dart';
import 'package:fine_arts_mobile/features/auth/auth.dart';
import 'package:fine_arts_mobile/features/notifications/notifications.dart';
import 'package:fine_arts_mobile/features/notifications/notifications_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

class _FakeAuth extends AuthController {
  @override
  Future<AppUser?> build() async =>
      const AppUser(accountId: 1, username: 'student01', fullName: 'Nguyen Van An', role: Role.student);
}

/// Notifications API stand-in: remembers what was marked as read.
class _FakeRepo implements NotificationRepository {
  final read = <int>[];
  bool allRead = false;

  final items = [
    AppNotification(
      id: 1,
      title: 'Results are published',
      message: 'The results of Spring Landscapes are published.',
      link: '/student/results',
      isRead: false,
      createdAt: DateTime(2026, 11, 20, 9, 30),
    ),
    AppNotification(
      id: 2,
      title: 'Deadline reminder',
      message: 'Spring Landscapes closes in 3 days.',
      link: null,
      isRead: true,
      createdAt: DateTime(2026, 11, 18, 8, 0),
    ),
  ];

  @override
  Future<NotificationPage> page(int page) async => NotificationPage(
        items: [for (final n in items) allRead || read.contains(n.id) ? n.copyWith(isRead: true) : n],
        page: 1,
        totalPages: 1,
        unreadCount: allRead ? 0 : 1 - read.length,
      );

  @override
  Future<int> unreadCount() async => allRead ? 0 : 1 - read.length;

  @override
  Future<int> markRead(int id) async {
    read.add(id);
    return 0;
  }

  @override
  Future<void> markAllRead() async => allRead = true;

  @override
  Future<void> registerDevice(String token, String platform) async {}

  @override
  Future<void> unregisterDevice(String token) async {}
}

Future<_FakeRepo> _pump(WidgetTester tester) async {
  final repo = _FakeRepo();
  final router = GoRouter(
    initialLocation: '/home',
    routes: [
      GoRoute(
        path: '/home',
        builder: (c, s) => Scaffold(appBar: AppBar(actions: const [NotificationBell()])),
      ),
      GoRoute(path: '/notifications', builder: (c, s) => const NotificationsScreen()),
      GoRoute(path: '/student/entries', builder: (c, s) => const Text('MY ENTRIES')),
    ],
  );
  tester.view.physicalSize = const Size(400, 800);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(ProviderScope(
    overrides: [
      authControllerProvider.overrideWith(_FakeAuth.new),
      notificationRepositoryProvider.overrideWithValue(repo),
    ],
    child: MaterialApp.router(theme: buildTheme(), routerConfig: router),
  ));
  await tester.pumpAndSettle();
  return repo;
}

void main() {
  test('website links open the matching app screen, unknown ones none', () {
    expect(appRouteFor('/student/results'), '/student/entries');
    expect(appRouteFor('/student/awards'), '/student/entries');
    expect(appRouteFor('/student/competitions/7'), '/competitions/7');
    expect(appRouteFor('/student/exhibition-entries/12'), '/student/exhibition-entries/12');
    expect(appRouteFor('/staff/reports/3'), isNull);
    expect(appRouteFor(null), isNull);
  });

  testWidgets('the bell shows the unread count and opens the list', (tester) async {
    await _pump(tester);
    expect(find.byTooltip('Notifications, 1 unread'), findsOneWidget);
    expect(find.text('1'), findsOneWidget);

    await tester.tap(find.byType(NotificationBell));
    await tester.pump();
    await tester.pump(AppMotion.handOverIn);
    await tester.pump(AppMotion.handOverIn);
    expect(find.text('Results are published'), findsOneWidget);
    expect(find.text('Deadline reminder'), findsOneWidget);
    expect(find.text('20 Nov 2026 09:30'), findsOneWidget);
  });

  testWidgets('tapping a notification marks it read and opens its screen; mark all read', (tester) async {
    final repo = await _pump(tester);
    await tester.tap(find.byType(NotificationBell));
    await tester.pump();
    await tester.pump(AppMotion.handOverIn);
    await tester.pump(AppMotion.handOverIn);

    expect(find.text('Mark all read'), findsOneWidget);
    await tester.tap(find.text('Results are published'));
    await tester.pumpAndSettle();
    expect(repo.read, [1]);
    expect(find.text('MY ENTRIES'), findsOneWidget);

    // Back on the list nothing is unread any more
    tester.state<NavigatorState>(find.byType(Navigator).last).pop();
    await tester.pumpAndSettle();
    expect(find.text('Mark all read'), findsNothing);
  });

  testWidgets('mark all read clears the badge', (tester) async {
    final repo = await _pump(tester);
    await tester.tap(find.byType(NotificationBell));
    await tester.pump();
    await tester.pump(AppMotion.handOverIn);
    await tester.pump(AppMotion.handOverIn);

    await tester.tap(find.text('Mark all read'));
    await tester.pumpAndSettle();
    expect(repo.allRead, isTrue);
    expect(find.text('Mark all read'), findsNothing);
  });
}
