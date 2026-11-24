import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:fine_arts_mobile/features/grading/grading.dart';
import 'package:fine_arts_mobile/features/submissions/submissions.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  const criteria = [
    GradingCriterion(id: 1, name: 'Creativity', weight: 60),
    GradingCriterion(id: 2, name: 'Technique', weight: 40),
  ];

  group('parseScore', () {
    test('0–100 with at most 2 decimals, comma or dot', () {
      expect(parseScore('85'), 85);
      expect(parseScore(' 85,5 '), 85.5);
      expect(parseScore('100'), 100);
      expect(parseScore('0.25'), 0.25);
    });

    test('anything else is invalid', () {
      for (final bad in ['', 'abc', '101', '-1', '80.125', '1e2']) {
        expect(parseScore(bad), isNull, reason: bad);
      }
    });
  });

  test('judgeTotal is weighted and needs every score', () {
    expect(judgeTotal(criteria, {1: 95, 2: 80}), closeTo(89, 1e-9));
    expect(judgeTotal(criteria, {1: 95, 2: null}), isNull);
    expect(judgeTotal(const [], {}), isNull);
  });

  group('validateEvaluation (BR-23)', () {
    String? check({bool dq = false, Map<int, String>? scores, String positive = 'Good', String negative = ''}) =>
        validateEvaluation(
          disqualified: dq,
          criteria: criteria,
          scoreTexts: scores ?? {1: '90', 2: '80'},
          positive: positive,
          negative: negative,
        );

    test('every criterion needs a valid score', () {
      expect(check(scores: {1: '90'}), 'Give Technique a score from 0 to 100 (at most 2 decimals)');
      expect(check(scores: {1: '120', 2: '80'}), contains('Creativity'));
    });

    test('scored entries need the strengths', () {
      expect(check(positive: '  '), 'Please enter the strengths');
      expect(check(), isNull);
    });

    test('a disqualification needs only its reason', () {
      expect(check(dq: true, negative: ' ', scores: {}), 'A disqualification needs a reason');
      expect(check(dq: true, negative: 'Copied', scores: {}, positive: ''), isNull);
    });
  });

  test('progressLabel dạng x/y', () {
    expect(progressLabel(18, 25), '18/25');
  });

  group('GradingFilter', () {
    test('toQuery chỉ gửi điều kiện đang bật, xếp loại dạng chữ hoa đầu', () {
      expect(const GradingFilter().toQuery(), isEmpty);
      final q = const GradingFilter(graded: false, rating: SubmissionRating.best, q: ' Lan ').toQuery();
      expect(q, {'graded': false, 'rating': 'Best', 'q': 'Lan'});
    });

    test('so sánh theo giá trị để dùng làm khóa provider', () {
      expect(const GradingFilter(graded: true, q: 'a'), const GradingFilter(graded: true, q: 'a'));
      expect(const GradingFilter(graded: true).hashCode, const GradingFilter(graded: true).hashCode);
      expect(const GradingFilter(graded: true), isNot(const GradingFilter(graded: false)));
    });
  });

  group('EvaluationInput.toJson', () {
    test('scores with comments; no rowVersion on a first grade', () {
      const input = EvaluationInput(
        scores: [GradingScore(criterionId: 1, score: 90, comment: ' Fresh '), GradingScore(criterionId: 2, score: 80)],
        positivePoints: 'tốt',
        negativePoints: '',
        improvementAreas: '',
      );
      final json = input.toJson();
      expect(json['disqualified'], isFalse);
      expect(json['scores'], [
        {'criterionId': 1, 'score': 90.0, 'comment': 'Fresh'},
        {'criterionId': 2, 'score': 80.0},
      ]);
      expect(json.containsKey('rowVersion'), isFalse);
    });

    test('a disqualification sends no scores; an edit sends rowVersion', () {
      const input = EvaluationInput(
        disqualified: true,
        scores: [GradingScore(criterionId: 1, score: 90)],
        positivePoints: '',
        negativePoints: 'Copied',
        improvementAreas: '',
        rowVersion: 'AAAA',
      );
      final json = input.toJson();
      expect(json['scores'], isEmpty);
      expect(json['rowVersion'], 'AAAA');
    });
  });

  group('parse JSON từ API', () {
    test('GradingDetail: criteria, jury, own grade, result, pictures', () {
      final d = GradingDetail.fromJson({
        'id': 7,
        'competitionId': 2,
        'competitionTitle': 'Mùa thu',
        'competitionStatus': 'Closed',
        'title': 'Bài A',
        'description': 'Mô tả',
        'textType': 'Poem',
        'textContent': 'Thơ',
        'studentName': 'Lê Hoàng Nam',
        'studentCode': 'HV001',
        'submittedAt': '2026-11-01T10:00:00',
        'updatedAt': null,
        'canEvaluate': true,
        'canDeleteEvaluation': true,
        'lockReason': null,
        'criteria': [
          {'id': 1, 'name': 'Creativity', 'description': null, 'weight': 60},
          {'id': 2, 'name': 'Technique', 'weight': 40.5},
        ],
        'jury': [
          {'name': 'Nguyễn Thu Hà', 'isChair': true, 'done': true, 'isMe': true},
          {'name': 'Vo Quoc Bao', 'isChair': false, 'done': false, 'isMe': false},
        ],
        'evaluation': {
          'disqualified': false,
          'scores': [
            {'criterionId': 1, 'score': 90, 'comment': 'ok'},
            {'criterionId': 2, 'score': 85.5},
          ],
          'totalScore': 88.2,
          'positivePoints': 'tốt',
          'negativePoints': null,
          'improvementAreas': null,
          'evaluatedByName': 'Nguyễn Thu Hà',
          'evaluatedAt': '2026-11-09T08:00:00',
          'rowVersion': 'AAAAAAAAB9E=',
        },
        'resultRating': 'Better',
        'resultScore': 88.2,
        'scoreSheet': null,
        'imageIds': [11, 12],
        'nextUnevaluatedId': 8,
        'total': 2,
        'evaluated': 1,
      });
      expect(d.competitionStatus, CompetitionStatus.closed);
      expect(d.criteria.map((c) => c.weight), [60, 40.5]);
      expect(d.jury.first.isMe, isTrue);
      expect(d.evaluation!.scores[1].score, 85.5);
      expect(d.evaluation!.totalScore, 88.2);
      expect(d.evaluation!.rowVersion, 'AAAAAAAAB9E=');
      expect(d.resultRating, SubmissionRating.better);
      expect(d.imageIds, [11, 12]);
      expect(d.nextUnevaluatedId, 8);
      expect(d.lockReason, isNull);
    });

    test('ScoreSheet with judges and averages', () {
      final s = ScoreSheet.fromJson({
        'criteria': [
          {'id': 1, 'name': 'Creativity', 'weight': 100},
        ],
        'judges': [
          {
            'name': 'Hà',
            'isChair': true,
            'disqualified': false,
            'totalScore': 92,
            'scores': [
              {'criterionId': 1, 'score': 92},
            ],
          },
          {'name': 'Bảo', 'disqualified': true, 'totalScore': null, 'scores': [], 'negativePoints': 'Copied'},
        ],
        'averages': [
          {'criterionId': 1, 'score': 92},
        ],
        'totalScore': null,
        'rating': 'Disqualified',
      });
      expect(s.judges, hasLength(2));
      expect(s.judges.last.disqualified, isTrue);
      expect(s.rating, SubmissionRating.disqualified);
      expect(s.averages.single.score, 92);
    });

    test('GradingItem: graded by me, and the result once the whole jury graded', () {
      final pending = GradingItem.fromJson({
        'id': 1,
        'title': 'A',
        'studentName': 'X',
        'studentCode': 'HV1',
        'submittedAt': '2026-11-01T10:00:00',
        'rating': null,
        'gradedByMe': true,
      });
      expect(pending.isEvaluated, isFalse);
      expect(pending.gradedByMe, isTrue);
    });
  });
}
