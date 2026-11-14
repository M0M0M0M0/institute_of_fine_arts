import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:fine_arts_mobile/core/api_client.dart';
import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:fine_arts_mobile/features/submissions/submissions.dart';
import 'package:flutter_test/flutter_test.dart';

// Kiểm thử parse JSON bài dự thi theo hợp đồng API, lỗi 409 và các hàm thuần.

Map<String, dynamic> _listItem({Object? rating = 'Best', bool canEdit = true}) => {
      'id': 7,
      'competitionId': 2,
      'competitionTitle': 'Vẽ mùa xuân',
      'competitionStatus': 'Graded',
      'title': 'Hoa đào',
      'submittedAt': '2026-03-01T09:30:00',
      'updatedAt': null,
      'canEdit': canEdit,
      'rating': rating,
    };

Map<String, dynamic> _detail({Object? evaluation, List<Object> awards = const []}) => {
      'id': 7,
      'competitionId': 2,
      'competitionTitle': 'Vẽ mùa xuân',
      'competitionStatus': 'Ongoing',
      'title': 'Hoa đào',
      'description': 'Mô tả',
      'textType': 'Poem',
      'textContent': 'Xuân về',
      'publicConsent': true,
      'originalFileName': 'hoa.jpg',
      'submittedAt': '2026-03-01T09:30:00',
      'updatedAt': '2026-03-02T10:00:00',
      'canEdit': true,
      'evaluation': evaluation,
      'awards': awards,
    };

DioException _dioError(int status, Object data) => DioException(
      requestOptions: RequestOptions(path: '/x'),
      response: Response(requestOptions: RequestOptions(path: '/x'), statusCode: status, data: data),
    );

void main() {
  group('SubmissionListItem', () {
    test('parse đầy đủ có xếp loại', () {
      final s = SubmissionListItem.fromJson(_listItem());
      expect(s.id, 7);
      expect(s.competitionStatus, CompetitionStatus.graded);
      expect(s.rating, SubmissionRating.best);
      expect(s.updatedAt, isNull);
      expect(s.submittedAt, DateTime(2026, 3, 1, 9, 30));
    });

    test('rating null và canEdit false', () {
      final s = SubmissionListItem.fromJson(_listItem(rating: null, canEdit: false));
      expect(s.rating, isNull);
      expect(s.canEdit, isFalse);
    });
  });

  group('SubmissionDetail', () {
    test('chưa chấm: evaluation null, awards rỗng', () {
      final d = SubmissionDetail.fromJson(_detail());
      expect(d.evaluation, isNull);
      expect(d.awards, isEmpty);
      expect(d.textType, TextType.poem);
      expect(d.updatedAt, DateTime(2026, 3, 2, 10));
    });

    test('đã chấm có nhận xét (một số trường null) và giải', () {
      final d = SubmissionDetail.fromJson(_detail(
        evaluation: {
          'rating': 'Disqualified',
          'positivePoints': 'Màu đẹp',
          'negativePoints': null,
          'improvementAreas': null,
          'evaluatedAt': '2026-03-10T08:00:00',
        },
        awards: [
          {'awardName': 'Giải nhất', 'rank': 1},
        ],
      ));
      expect(d.evaluation!.rating, SubmissionRating.disqualified);
      expect(d.evaluation!.positivePoints, 'Màu đẹp');
      expect(d.evaluation!.negativePoints, isNull);
      expect(d.awards.single.awardName, 'Giải nhất');
      expect(d.awards.single.rank, 1);
    });

    test('thiếu trường awards vẫn parse được', () {
      final json = _detail()..remove('awards');
      expect(SubmissionDetail.fromJson(json).awards, isEmpty);
    });
  });

  test('AwardResult parse (prizeValue số thực hoặc nguyên)', () {
    final a = AwardResult.fromJson({
      'awardName': 'Giải nhất',
      'rank': 1,
      'studentName': 'Nguyễn An',
      'paintingTitle': 'Hoa đào',
      'prizeValue': 2000000.0,
    });
    expect(a.studentName, 'Nguyễn An');
    expect(a.prizeValue, 2000000);
  });

  group('Nhãn và giá trị API', () {
    test('nhãn xếp loại song ngữ', () {
      expect(SubmissionRating.best.label, 'Xuất sắc nhất (BEST)');
      expect(SubmissionRating.better.label, 'Tốt hơn (BETTER)');
      expect(SubmissionRating.good.label, 'Tốt (GOOD)');
      expect(SubmissionRating.moderate.label, 'Khá (MODERATE)');
      expect(SubmissionRating.normal.label, 'Bình thường (NORMAL)');
      expect(SubmissionRating.disqualified.label, 'Bị loại (DISQUALIFIED)');
    });

    test('apiValue viết hoa chữ đầu', () {
      expect(SubmissionRating.disqualified.apiValue, 'Disqualified');
      expect(TextType.quotation.apiValue, 'Quotation');
      expect(TextType.story.label, 'Câu chuyện');
    });
  });

  group('validateImageFile', () {
    test('chấp nhận jpg/jpeg không phân biệt hoa thường', () {
      expect(validateImageFile('a.jpg', 100), isNull);
      expect(validateImageFile('A.JPEG', 100), isNull);
    });

    test('từ chối định dạng khác', () {
      expect(validateImageFile('a.png', 100), 'Chỉ chấp nhận ảnh JPEG');
      expect(validateImageFile('jpg', 100), 'Chỉ chấp nhận ảnh JPEG');
    });

    test('giới hạn 5 MB: đúng 5 MB được, hơn 1 byte thì không', () {
      expect(validateImageFile('a.jpg', maxImageBytes), isNull);
      expect(validateImageFile('a.jpg', maxImageBytes + 1), 'Ảnh vượt quá 5 MB');
    });

    test('từ chối file rỗng', () {
      expect(validateImageFile('a.jpg', 0), 'File ảnh rỗng, vui lòng chọn ảnh khác');
    });
  });

  group('smallImageWarning (BR-17)', () {
    test('cạnh ngắn từ 600 px trở lên thì không cảnh báo', () {
      expect(smallImageWarning(600, 600), isNull);
      expect(smallImageWarning(4000, 600), isNull);
    });

    test('cạnh ngắn dưới 600 px thì cảnh báo, không phụ thuộc chiều ngang/dọc', () {
      expect(smallImageWarning(599, 2000), contains('599×2000'));
      expect(smallImageWarning(2000, 599), contains('2000×599'));
    });
  });

  group('deadlineLabel', () {
    test('đang diễn ra', () {
      expect(deadlineLabel(CompetitionStatus.ongoing, 3), 'Còn 3 ngày');
      expect(deadlineLabel(CompetitionStatus.ongoing, 0), 'Hôm nay là hạn nộp cuối');
    });

    test('đã đóng hoặc đã công bố', () {
      expect(deadlineLabel(CompetitionStatus.closed, 0), 'Đã hết hạn nộp bài');
      expect(deadlineLabel(CompetitionStatus.graded, 0), 'Đã hết hạn nộp bài');
    });

    test('sắp diễn ra không có nhãn', () {
      expect(deadlineLabel(CompetitionStatus.upcoming, 10), isNull);
    });
  });

  group('SubmissionFilter', () {
    test('query chỉ gồm điều kiện đang bật, ngày yyyy-MM-dd', () {
      final f = SubmissionFilter(
        competitionId: 2,
        q: ' hoa ',
        from: DateTime(2026, 3, 1),
        to: DateTime(2026, 3, 31),
        rating: SubmissionRating.good,
      );
      expect(f.toQuery(), {
        'competitionId': 2,
        'q': 'hoa',
        'from': '2026-03-01',
        'to': '2026-03-31',
        'rating': 'Good',
      });
      expect(const SubmissionFilter().toQuery(), isEmpty);
      expect(const SubmissionFilter().isActive, isFalse);
    });

    test('so sánh bằng giá trị và copyWith xóa được trường', () {
      const a = SubmissionFilter(competitionId: 1);
      expect(a, const SubmissionFilter(competitionId: 1));
      expect(a.copyWith(competitionId: () => null), const SubmissionFilter());
    });
  });

  group('ApiException extensions', () {
    test('409 có submissionId', () {
      final e = ApiException.from(_dioError(409, {
        'title': 'Bạn đã nộp bài cho cuộc thi này',
        'status': 409,
        'submissionId': 12,
      }));
      expect(e.statusCode, 409);
      expect(e.message, 'Bạn đã nộp bài cho cuộc thi này');
      expect(e.submissionId, 12);
      expect(e.extensions.containsKey('title'), isFalse);
    });

    test('submissionId dạng chuỗi vẫn đọc được', () {
      expect(ApiException.from(_dioError(409, {'title': 'x', 'submissionId': '5'})).submissionId, 5);
    });

    test('lỗi không có extension: submissionId null, hành vi cũ giữ nguyên', () {
      final e = ApiException.from(_dioError(400, {
        'title': 'One or more validation errors occurred.',
        'errors': {
          'Title': ['Tiêu đề quá dài'],
        },
      }));
      expect(e.message, 'Tiêu đề quá dài');
      expect(e.submissionId, isNull);
      expect(e.extensions, isEmpty);
    });

    test('thân lỗi dạng byte (ResponseType.bytes) vẫn đọc được ProblemDetails', () {
      final body = utf8.encode('{"title":"Không tìm thấy ảnh","status":404}');
      final e = ApiException.from(_dioError(404, body));
      expect(e.message, 'Không tìm thấy ảnh');
      expect(e.statusCode, 404);
    });
  });
}
