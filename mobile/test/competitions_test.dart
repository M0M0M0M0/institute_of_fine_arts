import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:flutter_test/flutter_test.dart';

// Kiểm thử parse JSON cuộc thi và countdown label "Starts in X days".
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
  test('CompetitionDetail reads the timeline (older API without it: empty)', () {
    final base = {
      'id': 7, 'title': 'Spring', 'startDate': '2026-11-01', 'endDate': '2026-12-20', 'status': 'Ongoing',
      'daysLeft': 3, 'daysUntilStart': 0, 'submissionCount': 2, 'deadline': '2026-12-20T23:59:59', 'awards': <dynamic>[],
    };
    expect(CompetitionDetail.fromJson(base).timeline, isEmpty);
    final d = CompetitionDetail.fromJson({
      ...base,
      'timeline': [
        {'date': '2026-11-01', 'title': 'Submissions open', 'content': null, 'isSystem': true, 'state': 'Past'},
        {'date': '2026-11-25', 'title': 'Award ceremony', 'content': 'Main hall', 'isSystem': false, 'state': 'Current'},
        {'date': '2026-12-20', 'title': 'Submissions close', 'isSystem': true, 'state': 'Upcoming'},
      ],
    });
    expect(d.timeline.map((t) => t.state), [TimelineState.past, TimelineState.current, TimelineState.upcoming]);
    expect(d.timeline[1].content, 'Main hall');
    expect(d.timeline.first.isSystem, isTrue);
  });

  test('parse daysUntilStart từ JSON', () {
    final c = CompetitionSummary.fromJson(_json());
    expect(c.daysUntilStart, 5);
    expect(c.startCountdownLabel, 'Starts in 5 days');
  });

  test('thiếu daysUntilStart thì mặc định 0', () {
    final c = CompetitionSummary.fromJson(_json(withField: false));
    expect(c.daysUntilStart, 0);
    expect(c.startCountdownLabel, 'Starts today');
  });

  test('daysUntilStart null cũng mặc định 0', () {
    expect(CompetitionSummary.fromJson(_json(daysUntilStart: null)).daysUntilStart, 0);
  });

  test('cuộc thi không phải UPCOMING không có nhãn đếm ngược', () {
    expect(CompetitionSummary.fromJson(_json(status: 'Ongoing')).startCountdownLabel, isNull);
  });
}
