import 'dart:js_interop';
import 'dart:typed_data';

import 'package:web/web.dart' as web;

/// Bản cho web: tạo Blob từ byte, gắn vào thẻ <a download> rồi bấm giả để trình duyệt tải về.
Future<bool> saveBytesAsFile(Uint8List bytes, String fileName, {String mimeType = 'image/jpeg'}) async {
  // Blob chứa dữ liệu ảnh với đúng content-type
  final blob = web.Blob([bytes.toJS].toJS, web.BlobPropertyBag(type: mimeType));
  final url = web.URL.createObjectURL(blob);
  // Thẻ <a> ẩn có thuộc tính download = tên file gốc
  final anchor = web.HTMLAnchorElement()
    ..href = url
    ..download = fileName
    ..style.display = 'none';
  web.document.body!.append(anchor);
  anchor.click();
  anchor.remove();
  // Giải phóng URL tạm
  web.URL.revokeObjectURL(url);
  return true;
}
