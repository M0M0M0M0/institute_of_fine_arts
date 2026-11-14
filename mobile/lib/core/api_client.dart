import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'config.dart';
import 'token_storage.dart';

/// Nơi lưu token, dùng chung toàn app.
final tokenStorageProvider = Provider<TokenStorage>((ref) => TokenStorage());

/// Dio dùng chung: base URL của API + tự gắn header Authorization nếu đã đăng nhập.
final dioProvider = Provider<Dio>((ref) {
  final storage = ref.watch(tokenStorageProvider);
  final dio = Dio(BaseOptions(
    baseUrl: apiBaseUrl,
    connectTimeout: const Duration(seconds: 10),
    receiveTimeout: const Duration(seconds: 20),
  ));

  dio.interceptors.add(InterceptorsWrapper(
    onRequest: (options, handler) async {
      final token = await storage.read();
      if (token != null) {
        options.headers['Authorization'] = 'Bearer $token';
      }
      handler.next(options);
    },
  ));
  return dio;
});

/// Lỗi gọi API đã được dịch sang thông báo tiếng Việt để hiển thị thẳng cho người dùng.
class ApiException implements Exception {
  ApiException(this.message, {this.statusCode, this.extensions = const {}});

  final String message;
  final int? statusCode;

  /// Các trường mở rộng của ProblemDetails (ví dụ `submissionId` khi 409), không gồm trường chuẩn.
  final Map<String, dynamic> extensions;

  /// Mã bài dự thi đã tồn tại mà API gắn kèm khi trả 409 (BR-15); null nếu không có.
  int? get submissionId {
    final v = extensions['submissionId'];
    if (v is num) return v.toInt();
    if (v is String) return int.tryParse(v);
    return null;
  }

  /// Các khóa chuẩn của ProblemDetails, không coi là "extension".
  static const _standardKeys = {'type', 'title', 'status', 'detail', 'instance', 'errors', 'traceId'};

  /// Đọc ProblemDetails mà API trả về: ưu tiên lỗi validate từng trường, sau đó tới "title".
  factory ApiException.from(Object error) {
    if (error is ApiException) return error;
    if (error is DioException) {
      final response = error.response;
      if (response == null) {
        return ApiException('Không kết nối được máy chủ. Kiểm tra API đã chạy chưa.');
      }
      String? message;
      Object? data = response.data;
      // Khi gọi với ResponseType.bytes, thân lỗi là mảng byte → thử giải mã JSON
      if (data is List<int>) {
        try {
          data = jsonDecode(utf8.decode(data));
        } catch (_) {
          data = null;
        }
      }
      final extensions = <String, dynamic>{};
      if (data is Map) {
        message = (data['title'] ?? data['detail'])?.toString();
        final errors = data['errors'];
        // `title` của API đã là tiếng Việt (vd: "Ảnh vượt quá 5 MB" đi kèm `errors` tiếng Anh của framework),
        // nên chỉ lấy lỗi từng trường khi thiếu title hoặc title là câu mặc định của ASP.NET Core
        final titleText = data['title']?.toString() ?? '';
        final isDefaultTitle = titleText.isEmpty || titleText.startsWith('One or more validation errors');
        if (isDefaultTitle && errors is Map && errors.isNotEmpty) {
          final first = errors.values.first;
          if (first is List && first.isNotEmpty) message = first.first.toString();
        }
        // Giữ lại các trường mở rộng để màn hình xử lý riêng (vd: submissionId)
        data.forEach((k, v) {
          if (!_standardKeys.contains(k)) extensions[k.toString()] = v;
        });
      }
      return ApiException(message ?? 'Lỗi máy chủ (${response.statusCode})',
          statusCode: response.statusCode, extensions: extensions);
    }
    return ApiException('Đã có lỗi xảy ra: $error');
  }

  @override
  String toString() => message;
}

/// Bọc một lời gọi API: mọi lỗi đều được đổi thành ApiException.
Future<T> guardApi<T>(Future<T> Function() call) async {
  try {
    return await call();
  } catch (e) {
    throw ApiException.from(e);
  }
}
