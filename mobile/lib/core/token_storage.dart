import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Keeps the JWT so the app does not ask to sign in on every start.
///
/// The token opens the user's account for days, so it is kept in the platform's secure store (Android Keystore,
/// iOS Keychain; encrypted browser storage on the web), not in plain SharedPreferences, where a backup or a rooted
/// phone could read it (review 26/11/2026, B8). A token saved by an older version of the app in SharedPreferences is
/// moved over once and removed from there.
///
/// Never throws: when the secure store is not available (a damaged keystore, or widget tests without the plugin)
/// it falls back to SharedPreferences, so signing in still works instead of every request hanging.
class TokenStorage {
  static const _key = 'auth_token';

  final FlutterSecureStorage _secure = const FlutterSecureStorage();

  Future<String?> read() async {
    try {
      final token = await _secure.read(key: _key);
      if (token != null) return token;
    } catch (e) {
      debugPrint('Secure storage unavailable, using SharedPreferences: $e');
      return (await SharedPreferences.getInstance()).getString(_key);
    }
    // Older app versions kept it in SharedPreferences: move it once
    final prefs = await SharedPreferences.getInstance();
    final old = prefs.getString(_key);
    if (old == null) return null;
    try {
      await _secure.write(key: _key, value: old);
      await prefs.remove(_key);
    } catch (_) {
      // keep it where it is
    }
    return old;
  }

  Future<void> write(String token) async {
    try {
      await _secure.write(key: _key, value: token);
    } catch (e) {
      debugPrint('Secure storage unavailable, using SharedPreferences: $e');
      await (await SharedPreferences.getInstance()).setString(_key, token);
    }
  }

  Future<void> clear() async {
    try {
      await _secure.delete(key: _key);
    } catch (_) {
      // nothing stored there
    }
    await (await SharedPreferences.getInstance()).remove(_key);
  }
}
