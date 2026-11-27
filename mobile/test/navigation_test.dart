import 'package:fine_arts_mobile/features/notifications/notifications.dart';
import 'package:fine_arts_mobile/features/auth/auth.dart';
import 'package:fine_arts_mobile/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Auth controller that starts with a fixed user (or none) without calling the API.
class _FakeAuth extends AuthController {
  _FakeAuth(this._user);

  final AppUser? _user;

  @override
  Future<AppUser?> build() async => _user;
}

Future<void> _pump(WidgetTester tester, AppUser? user) async {
  SharedPreferences.setMockInitialValues({});
  FlutterSecureStorage.setMockInitialValues({});
  tester.view.physicalSize = const Size(412, 915);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(ProviderScope(
    overrides: [
      authControllerProvider.overrideWith(() => _FakeAuth(user)),
      unreadCountProvider.overrideWith((ref) async => 0),
    ],
    child: const FineArtsApp(),
  ));
  await tester.pumpAndSettle();
}

AppUser _user(Role role) => AppUser(accountId: 1, username: 'u', fullName: 'Nguyen Van An', role: role);

void main() {
  testWidgets('guest bottom bar: Home, Competitions, Gallery, Sign in', (tester) async {
    await _pump(tester, null);
    final bar = find.byType(NavigationBar);
    for (final label in ['Home', 'Competitions', 'Gallery', 'Sign in']) {
      expect(find.descendant(of: bar, matching: find.text(label)), findsOneWidget);
    }
    await tester.tap(find.descendant(of: bar, matching: find.text('Gallery')));
    await tester.pumpAndSettle();
    expect(find.text('Gallery'), findsWidgets);
    expect(find.byType(AppBar), findsOneWidget);

    await tester.tap(find.descendant(of: bar, matching: find.text('Sign in')));
    await tester.pumpAndSettle();
    expect(find.text('Enter competitions and follow your entries.'), findsOneWidget); // login screen
  });

  testWidgets('student bottom bar has five tabs and the account tab shows the profile', (tester) async {
    await _pump(tester, _user(Role.student));
    final bar = find.byType(NavigationBar);
    for (final label in ['Home', 'Competitions', 'My entries', 'Exhibitions', 'Account']) {
      expect(find.descendant(of: bar, matching: find.text(label)), findsOneWidget);
    }
    await tester.tap(find.descendant(of: bar, matching: find.text('Account')));
    await tester.pumpAndSettle();
    expect(find.text('Nguyen Van An'), findsOneWidget);
    expect(find.text('SIGN OUT'), findsOneWidget);
  });

  testWidgets('staff bottom bar: Home, Grading, Awards, Exhibitions, Account', (tester) async {
    await _pump(tester, _user(Role.staff));
    final bar = find.byType(NavigationBar);
    for (final label in ['Home', 'Grading', 'Awards', 'Exhibitions', 'Account']) {
      expect(find.descendant(of: bar, matching: find.text(label)), findsOneWidget);
    }
    await tester.tap(find.descendant(of: bar, matching: find.text('Grading')));
    await tester.pumpAndSettle();
    expect(find.byType(AppBar), findsOneWidget);
  });

  testWidgets('teacher bottom bar: Grading, Account', (tester) async {
    await _pump(tester, _user(Role.teacher));
    final bar = find.byType(NavigationBar);
    for (final label in ['Grading', 'Account']) {
      expect(find.descendant(of: bar, matching: find.text(label)), findsOneWidget);
    }
    expect(find.descendant(of: bar, matching: find.text('Awards')), findsNothing);
  });

  testWidgets('admin is sent to the "use the website" screen', (tester) async {
    await _pump(tester, _user(Role.admin));
    expect(find.text('Please use the website'), findsOneWidget);
  });
}
