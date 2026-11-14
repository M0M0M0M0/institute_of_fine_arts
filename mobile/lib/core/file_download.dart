// Tải file về máy: web dùng thẻ <a download>, nền tảng khác dùng bản tạm (chưa hỗ trợ).
export 'file_download_stub.dart' if (dart.library.js_interop) 'file_download_web.dart';
