// Định dạng hiển thị theo NFR-17: ngày dd/MM/yyyy, tiền #.##0 ₫.

String _two(int n) => n.toString().padLeft(2, '0');

/// Ngày dạng dd/MM/yyyy.
String formatDate(DateTime d) => '${_two(d.day)}/${_two(d.month)}/${d.year}';

/// Ngày giờ dạng dd/MM/yyyy HH:mm.
String formatDateTime(DateTime d) => '${formatDate(d)} ${_two(d.hour)}:${_two(d.minute)}';

/// Tiền VNĐ, phân cách hàng nghìn bằng dấu chấm: 2000000 → "2.000.000 ₫".
String formatVnd(num value) {
  final digits = value.round().abs().toString();
  final buffer = StringBuffer();
  for (var i = 0; i < digits.length; i++) {
    if (i > 0 && (digits.length - i) % 3 == 0) buffer.write('.');
    buffer.write(digits[i]);
  }
  return '${value < 0 ? '-' : ''}$buffer ₫';
}
