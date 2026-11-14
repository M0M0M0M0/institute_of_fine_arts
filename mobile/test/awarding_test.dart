import 'package:fine_arts_mobile/features/awarding/awarding.dart';
import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:fine_arts_mobile/features/submissions/submissions.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('Hàm thuần của trao giải', () {
    test('validateReason: lý do rỗng hoặc toàn khoảng trắng bị từ chối (BR-34)', () {
      expect(validateReason('  '), 'Vui lòng nhập lý do');
      expect(validateReason('Chấm nhầm'), isNull);
    });

    test('slotsLabel dạng x/y', () {
      expect(slotsLabel(1, 4), '1/4');
    });

    test('emptySlotsMessage nêu số suất còn trống (BR-32)', () {
      expect(emptySlotsMessage(3), contains('3 suất'));
    });
  });

  group('Model khớp JSON của API', () {
    final json = {
      'id': 7,
      'title': 'Sắc màu mùa thu',
      'status': 'Closed',
      'submissionCount': 5,
      'evaluatedCount': 4,
      'pendingEvaluations': 1,
      'emptySlots': 3,
      'canAward': true,
      'canPublish': false,
      'publishBlockReason': 'Còn 1 bài chưa chấm',
      'isPublished': false,
      'publishedAt': null,
      'adjustedAt': null,
      'awards': [
        {
          'id': 1,
          'name': 'Giải Nhất',
          'rank': 1,
          'prizeValue': 2000000.0,
          'quantity': 1,
          'description': null,
          'activeCount': 1,
          'remaining': 0,
          'issued': [
            {
              'id': 9,
              'submissionId': 11,
              'submissionTitle': 'Biển lặng',
              'studentName': 'Lê Hoàng Nam',
              'studentCode': 'HV001',
              'rating': 'Best',
              'issuedDate': '2026-11-10',
              'remarks': null,
              'prizeValue': 2000000.0,
              'status': 'Active',
              'revokedReason': null,
              'isDelivered': false,
              'deliveredAt': null,
              'rowVersion': 'AAAAAAAAB9E=',
            },
          ],
        },
      ],
    };

    test('AwardingDetail đọc đủ checklist, giải và giải đã trao', () {
      final d = AwardingDetail.fromJson(json);
      expect(d.status, CompetitionStatus.closed);
      expect(d.pendingEvaluations, 1);
      expect(d.canPublish, isFalse);
      expect(d.publishBlockReason, 'Còn 1 bài chưa chấm');
      final issued = d.awards.single.issued.single;
      expect(issued.rating, SubmissionRating.best);
      expect(issued.status, IssuedStatus.active);
      expect(issued.isActive, isTrue);
      expect(issued.rowVersion, 'AAAAAAAAB9E=');
    });

    test('giải đã thu hồi không còn isActive', () {
      final revoked = Map<String, dynamic>.from((json['awards'] as List).first['issued'][0] as Map)
        ..['status'] = 'Revoked'
        ..['revokedReason'] = 'Sao chép';
      final a = IssuedAward.fromJson(revoked);
      expect(a.isActive, isFalse);
      expect(a.revokedReason, 'Sao chép');
    });
  });
}
