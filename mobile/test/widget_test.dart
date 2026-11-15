import 'package:fine_arts_mobile/core/format.dart';
import 'package:flutter_test/flutter_test.dart';

// Unit tests of the display formats (same as the website: English, USD). Run: flutter test
void main() {
  test('formatMoney: US dollars with comma separators, cents only when needed', () {
    expect(formatMoney(2000000), '\$2,000,000');
    expect(formatMoney(500), '\$500');
    expect(formatMoney(0), '\$0');
    expect(formatMoney(1234.5), '\$1,234.50');
    expect(formatMoney(-20), '-\$20');
  });

  test('formatDate as dd MMM yyyy', () {
    expect(formatDate(DateTime(2026, 3, 7)), '07 Mar 2026');
    expect(formatDateTime(DateTime(2026, 11, 9, 14, 5)), '09 Nov 2026 14:05');
  });
}
