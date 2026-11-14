import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:flutter_test/flutter_test.dart';

// Kiểm thử parse JSON cuộc thi và nhãn đếm ngược "Bắt đầu sau X ngày".
Map<String, dynamic> _json({String status = 'Upcoming', Object? daysUntilStart = 5, bool withField = true}) => {
      'id': 1,
      'title': 'Vẽ mùa xuân',
      'topic': null,
      'startDate': '2026-12-01T00:00:00',
      'endDate': '2026-12-20T00:00:00',
      'status': status,
      'daysLeft': 0,
      if (withField) 'daysUntilStart': daysUntilStart,
      'submissionCount': 0,
    };

void main() {
  test('parse daysUntilStart từ JSON', () {
    final c = CompetitionSummary.fromJson(_json());
    expect(c.daysUntilStart, 5);
    expect(c.startCountdownLabel, 'Bắt đầu sau 5 ngày');
  });

  test('thiếu daysUntilStart thì mặc định 0', () {
    final c = CompetitionSummary.fromJson(_json(withField: false));
    expect(c.daysUntilStart, 0);
    expect(c.startCountdownLabel, 'Bắt đầu hôm nay');
  });

  test('daysUntilStart null cũng mặc định 0', () {
    expect(CompetitionSummary.fromJson(_json(daysUntilStart: null)).daysUntilStart, 0);
  });

  test('cuộc thi không phải UPCOMING không có nhãn đếm ngược', () {
    expect(CompetitionSummary.fromJson(_json(status: 'Ongoing')).startCountdownLabel, isNull);
  });
}
