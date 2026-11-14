import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/token_storage.dart';

// ---------------- Model ----------------

/// Vai trò, khớp enum Role của API (trả về dạng chữ "Student", "Staff"...).
enum Role { admin, manager, staff, student }

Role roleFromJson(String value) =>
    Role.values.firstWhere((r) => r.name.toLowerCase() == value.toLowerCase());

/// Người dùng đang đăng nhập.
class AppUser {
  const AppUser({
    required this.accountId,
    required this.username,
    required this.fullName,
    required this.role,
  });

  final int accountId;
  final String username;
  final String fullName;
  final Role role;

  factory AppUser.fromJson(Map<String, dynamic> json) => AppUser(
        accountId: json['accountId'] as int,
        username: json['username'] as String,
        fullName: json['fullName'] as String,
        role: roleFromJson(json['role'] as String),
      );
}

// ---------------- Repository: gọi API xác thực ----------------

class AuthRepository {
  AuthRepository(this._dio, this._storage);

  final Dio _dio;
  final TokenStorage _storage;

  /// POST /api/auth/login → lưu token, trả về người dùng.
  Future<AppUser> login(String username, String password) => guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>(
          '/api/auth/login',
          data: {'username': username, 'password': password},
        );
        final body = res.data!;
        await _storage.write(body['token'] as String);
        return AppUser.fromJson(body['user'] as Map<String, dynamic>);
      });

  /// Khôi phục phiên khi mở app: có token thì gọi /api/auth/me để kiểm tra còn hạn.
  Future<AppUser?> restore() async {
    final token = await _storage.read();
    if (token == null) return null;
    try {
      final res = await _dio.get<Map<String, dynamic>>('/api/auth/me');
      return AppUser.fromJson(res.data!);
    } on DioException catch (e) {
      // Token hết hạn / tài khoản bị khóa → xóa token. Lỗi mạng → coi như chưa đăng nhập, giữ token.
      if (e.response?.statusCode == 401) await _storage.clear();
      return null;
    }
  }

  Future<void> logout() => _storage.clear();
}

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => AuthRepository(ref.watch(dioProvider), ref.watch(tokenStorageProvider)),
);

// ---------------- Controller: trạng thái đăng nhập của toàn app ----------------

/// null = chưa đăng nhập. Router lắng nghe provider này để chuyển trang.
final authControllerProvider =
    AsyncNotifierProvider<AuthController, AppUser?>(AuthController.new);

class AuthController extends AsyncNotifier<AppUser?> {
  @override
  Future<AppUser?> build() => ref.read(authRepositoryProvider).restore();

  Future<void> login(String username, String password) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(authRepositoryProvider).login(username, password),
    );
  }

  Future<void> logout() async {
    await ref.read(authRepositoryProvider).logout();
    state = const AsyncData(null);
  }
}

/// Lấy người dùng hiện tại (null nếu chưa đăng nhập, đang tải hoặc lỗi).
AppUser? currentUserOf(AsyncValue<AppUser?> auth) => switch (auth) {
      AsyncData(:final value) => value,
      _ => null,
    };
