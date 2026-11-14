import 'dart:typed_data';

/// Bản cho nền tảng không phải web: app chỉ chạy thử trên Chrome nên chưa hỗ trợ lưu file.
/// Trả về false để giao diện báo cho người dùng.
Future<bool> saveBytesAsFile(Uint8List bytes, String fileName, {String mimeType = 'image/jpeg'}) async => false;
