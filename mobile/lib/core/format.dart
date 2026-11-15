// Display formats, the same as the website (English, en-US): dates "09 Nov 2026", money in USD "$1,500".

const _months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

String _two(int n) => n.toString().padLeft(2, '0');

/// Date as "dd MMM yyyy": 2026-11-09 → "09 Nov 2026".
String formatDate(DateTime d) => '${_two(d.day)} ${_months[d.month - 1]} ${d.year}';

/// Date and time as "dd MMM yyyy HH:mm".
String formatDateTime(DateTime d) => '${formatDate(d)} ${_two(d.hour)}:${_two(d.minute)}';

/// Money in US dollars (A-19), thousands separated by commas, cents only when there are any:
/// 1500 → "$1,500", 1234.5 → "$1,234.50", -20 → "-$20".
String formatMoney(num value) {
  final cents = (value.abs() * 100).round();
  final whole = (cents ~/ 100).toString();
  final buffer = StringBuffer();
  for (var i = 0; i < whole.length; i++) {
    if (i > 0 && (whole.length - i) % 3 == 0) buffer.write(',');
    buffer.write(whole[i]);
  }
  final rest = cents % 100;
  final fraction = rest == 0 ? '' : '.${_two(rest)}';
  return '${value < 0 ? '-' : ''}\$$buffer$fraction';
}
