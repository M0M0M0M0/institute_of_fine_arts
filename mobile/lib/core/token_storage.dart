import 'package:shared_preferences/shared_preferences.dart';

/// Lưu JWT để mở lại app không phải đăng nhập lại.
/// Trên Flutter web, shared_preferences dùng localStorage của trình duyệt.
class TokenStorage {
  static const _key = 'auth_token';

  Future<String?> read() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_key);
  }

  Future<void> write(String token) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, token);
  }

  Future<void> clear() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_key);
  }
}
