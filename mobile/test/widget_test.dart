import 'package:fine_arts_mobile/core/format.dart';
import 'package:flutter_test/flutter_test.dart';

// Kiểm thử đơn vị cho hàm định dạng (NFR-17). Chạy: flutter test
void main() {
  test('formatVnd phân cách hàng nghìn bằng dấu chấm', () {
    expect(formatVnd(2000000), '2.000.000 ₫');
    expect(formatVnd(500), '500 ₫');
    expect(formatVnd(0), '0 ₫');
  });

  test('formatDate dạng dd/MM/yyyy', () {
    expect(formatDate(DateTime(2026, 3, 7)), '07/03/2026');
  });
}
