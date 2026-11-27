import 'package:fine_arts_mobile/core/api_client.dart';
import 'package:fine_arts_mobile/features/auth/auth.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

/// Repository stand-in: a saved session for a student, remembers sign-outs.
class _FakeRepo implements AuthRepository {
  int logouts = 0;

  @override
  bool restoreRefused = false;

  @override
  Future<AppUser?> restore() async =>
      const AppUser(accountId: 7, username: 'student01', fullName: 'Nguyen Van An', role: Role.student);

  @override
  Future<void> logout() async => logouts++;

  @override
  Future<AppUser> login(String username, String password) => throw UnimplementedError();
}

void main() {
  // Review 26/11/2026, A1/B5: when the server ends the session (account locked, role or password changed) the app
  // gets 401 on its next request; it must sign out instead of showing errors on every screen
  test('a 401 on a signed-in request signs the user out once', () async {
    final repo = _FakeRepo();
    final container = ProviderContainer(overrides: [authRepositoryProvider.overrideWithValue(repo)]);
    addTearDown(container.dispose);

    final user = await container.read(authControllerProvider.future);
    expect(user?.username, 'student01');

    container.read(sessionEndedProvider.notifier).signal();
    await Future<void>.delayed(Duration.zero);
    await Future<void>.delayed(Duration.zero);
    expect(currentUserOf(container.read(authControllerProvider)), isNull);
    expect(repo.logouts, 1);

    // Further 401s (other requests still in flight) change nothing
    container.read(sessionEndedProvider.notifier).signal();
    await Future<void>.delayed(Duration.zero);
    expect(repo.logouts, 1);
  });
}
