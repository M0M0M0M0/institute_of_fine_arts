import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:fine_arts_mobile/features/grading/grading.dart';
import 'package:fine_arts_mobile/features/submissions/submissions.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('validateEvaluation (BR-23)', () {
    test('bắt buộc chọn xếp loại', () {
      expect(validateEvaluation(null, 'tốt', ''), 'Vui lòng chọn xếp loại');
    });

    test('xếp loại thường bắt buộc điểm mạnh; điểm yếu và cải thiện không bắt buộc', () {
      expect(validateEvaluation(SubmissionRating.good, '  ', 'yếu'), 'Vui lòng nhập điểm mạnh');
      expect(validateEvaluation(SubmissionRating.good, 'Màu đẹp', ''), isNull);
    });

    test('DISQUALIFIED bắt buộc lý do, không cần điểm mạnh', () {
      expect(validateEvaluation(SubmissionRating.disqualified, 'tốt', ' '), 'Xếp loại DISQUALIFIED bắt buộc nhập lý do');
      expect(validateEvaluation(SubmissionRating.disqualified, '', 'Đạo văn'), isNull);
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
    test('tạo mới không gửi rowVersion', () {
      const input = EvaluationInput(
          rating: SubmissionRating.better, positivePoints: 'tốt', negativePoints: '', improvementAreas: '');
      final json = input.toJson();
      expect(json['rating'], 'Better');
      expect(json.containsKey('rowVersion'), isFalse);
    });

    test('sửa gửi kèm rowVersion', () {
      const input = EvaluationInput(
          rating: SubmissionRating.best,
          positivePoints: 'tốt',
          negativePoints: '',
          improvementAreas: '',
          rowVersion: 'AAAA');
      expect(input.toJson()['rowVersion'], 'AAAA');
    });
  });

  group('parse JSON từ API', () {
    test('GradingDetail: có đánh giá, bài kế tiếp và khóa chấm', () {
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
        'evaluation': {
          'rating': 'Better',
          'positivePoints': 'tốt',
          'negativePoints': null,
          'improvementAreas': null,
          'evaluatedByName': 'Nguyễn Thu Hà',
          'evaluatedAt': '2026-11-09T08:00:00',
          'rowVersion': 'AAAAAAAAB9E=',
        },
        'nextUnevaluatedId': 8,
        'total': 2,
        'evaluated': 1,
      });
      expect(d.competitionStatus, CompetitionStatus.closed);
      expect(d.evaluation!.rating, SubmissionRating.better);
      expect(d.evaluation!.rowVersion, 'AAAAAAAAB9E=');
      expect(d.nextUnevaluatedId, 8);
      expect(d.lockReason, isNull);
    });

    test('GradingItem.isEvaluated theo rating', () {
      final pending = GradingItem.fromJson({
        'id': 1,
        'title': 'A',
        'studentName': 'X',
        'studentCode': 'HV1',
        'submittedAt': '2026-11-01T10:00:00',
        'rating': null,
      });
      expect(pending.isEvaluated, isFalse);
    });
  });
}
