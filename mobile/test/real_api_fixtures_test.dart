import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:fine_arts_mobile/app/router.dart';
import 'package:fine_arts_mobile/core/api_client.dart';
import 'package:fine_arts_mobile/core/token_storage.dart';
import 'package:fine_arts_mobile/features/auth/auth.dart';
import 'package:fine_arts_mobile/features/competitions/competitions.dart';
import 'package:fine_arts_mobile/features/submissions/submissions.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

// Kiểm thử với JSON THẬT chụp từ API (không có token/bí mật): bảo đảm model Flutter đọc đúng
// enum dạng chữ, ngày không có múi giờ, prizeValue dạng số thập phân, evaluation null, ProblemDetails...

const _created =
    '{"id":3,"competitionId":1,"competitionTitle":"Sắc màu mùa thu","competitionStatus":"Ongoing","title":"Hoang hon","description":"Mo ta ngan","textType":"Poem","textContent":"Dong thoi gian","publicConsent":true,"originalFileName":"tiny.jpg","submittedAt":"2026-11-10T13:03:48.0565152","updatedAt":null,"canEdit":true,"evaluation":null,"awards":[]}';
const _updated =
    '{"id":3,"competitionId":1,"competitionTitle":"Sắc màu mùa thu","competitionStatus":"Ongoing","title":"Hoang hon 2","description":"Mo ta","textType":"Story","textContent":"zz","publicConsent":false,"originalFileName":"tiny.jpg","submittedAt":"2026-11-10T13:03:48.0565152","updatedAt":"2026-11-10T13:03:49.6107257","canEdit":true,"evaluation":null,"awards":[]}';
const _graded =
    '{"id":1,"competitionId":3,"competitionTitle":"Mùa hè xanh","competitionStatus":"Graded","title":"Biển lặng","description":"Bài mẫu","textType":"Poem","textContent":"Mẫu","publicConsent":true,"originalFileName":"seed.jpg","submittedAt":"2026-10-01T13:03:09.0624787","updatedAt":null,"canEdit":false,"evaluation":{"rating":"Best","positivePoints":"Màu sắc hài hòa","negativePoints":null,"improvementAreas":null,"evaluatedAt":"2026-10-16T13:03:09.0624787"},"awards":[{"awardName":"Giải Nhất","rank":1}]}';
const _myList =
    '[{"id":3,"competitionId":1,"competitionTitle":"Sắc màu mùa thu","competitionStatus":"Ongoing","title":"Hoang hon","submittedAt":"2026-11-10T13:03:48.0565152","updatedAt":null,"canEdit":true,"rating":null},{"id":1,"competitionId":3,"competitionTitle":"Mùa hè xanh","competitionStatus":"Graded","title":"Biển lặng","submittedAt":"2026-10-01T13:03:09.0624787","updatedAt":null,"canEdit":false,"rating":"Best"}]';
const _awards =
    '[{"awardName":"Giải Nhất","rank":1,"studentName":"Lê Hoàng Nam","paintingTitle":"Biển lặng","prizeValue":2000000.00},{"awardName":"Giải Nhì","rank":2,"studentName":"Phạm Ngọc Lan","paintingTitle":"Đỉnh núi sớm mai","prizeValue":1000000.00}]';
const _dup409 = '{"title":"Bạn đã có bài dự thi trong cuộc thi này, hãy sửa bài đã nộp","status":409,"submissionId":3}';
const _overdue403 =
    '{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.4","title":"Đã quá hạn nộp bài","status":403,"traceId":"00-c13e75566cd7ee284f7f52ae85364c0b-2ff75ff906f1c45b-00"}';
const _missing400 =
    '{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Vui lòng nhập tiêu đề","status":400,"traceId":"00-8e7065e4843df8161b7e3e2f077a7aa0-1731f5631b8cfb5b-00"}';
// Vượt giới hạn body: title tiếng Việt, errors là câu tiếng Anh của framework (key rỗng)
const _oversize400 =
    '{"title":"Ảnh vượt quá 5 MB","status":400,"errors":{"":["Failed to read the request form. Request body too large. The max request body size is 6291456 bytes."]}}';
const _image404 =
    '{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Không tìm thấy ảnh của bài dự thi","status":404,"traceId":"00-4e8992e462c902972fa2a3101750e92d-af90dab26b211a29-00"}';

Map<String, dynamic> _map(String s) => jsonDecode(s) as Map<String, dynamic>;

DioException _err(int status, Object body) {
  final req = RequestOptions(path: '/x');
  return DioException(
    requestOptions: req,
    response: Response(requestOptions: req, statusCode: status, data: body),
    type: DioExceptionType.badResponse,
  );
}

/// Adapter giả trả dữ liệu theo token trong header Authorization (mô phỏng 2 tài khoản).
class _FakeAdapter implements HttpClientAdapter {
  @override
  void close({bool force = false}) {}

  @override
  Future<ResponseBody> fetch(RequestOptions o, Stream<List<int>>? body, Future<void>? cancel) async {
    ResponseBody json(Object data) =>
        ResponseBody.fromString(jsonEncode(data), 200, headers: {
          Headers.contentTypeHeader: ['application/json'],
        });
    if (o.path == '/api/auth/login') {
      final u = (o.data as Map)['username'] as String;
      return json({
        'token': 'tok-$u',
        'expiresAtUtc': '2026-11-10T08:03:25Z',
        'user': {'accountId': u == 'a' ? 1 : 2, 'username': u, 'fullName': u, 'role': 'Student'},
      });
    }
    if (o.path == '/api/my/submissions') {
      return json(o.headers['Authorization'] == 'Bearer tok-a' ? jsonDecode(_myList) : []);
    }
    return json({});
  }
}

class _MemStorage extends TokenStorage {
  String? token;
  @override
  Future<String?> read() async => token;
  @override
  Future<void> write(String t) async => token = t;
  @override
  Future<void> clear() async => token = null;
}

void main() {
  group('Parse JSON thật', () {
    test('201 tạo bài: enum chữ, ngày không múi giờ, evaluation null', () {
      final d = SubmissionDetail.fromJson(_map(_created));
      expect(d.id, 3);
      expect(d.competitionStatus, CompetitionStatus.ongoing);
      expect(d.textType, TextType.poem);
      expect(d.submittedAt.year, 2026);
      expect(d.submittedAt.hour, 13); // giờ Việt Nam giữ nguyên, không bị cộng trừ múi giờ
      expect(d.submittedAt.isUtc, isFalse); // không có hậu tố Z → giờ địa phương
      expect(d.updatedAt, isNull);
      expect(d.canEdit, isTrue);
      expect(d.evaluation, isNull);
      expect(d.awards, isEmpty);
    });

    test('200 sửa bài: updatedAt có giá trị, textType Story', () {
      final d = SubmissionDetail.fromJson(_map(_updated));
      expect(d.textType, TextType.story);
      expect(d.publicConsent, isFalse);
      expect(d.updatedAt, isNotNull);
    });

    test('bài đã chấm: evaluation + giải, trường null được chấp nhận', () {
      final d = SubmissionDetail.fromJson(_map(_graded));
      expect(d.competitionStatus, CompetitionStatus.graded);
      expect(d.canEdit, isFalse);
      expect(d.evaluation!.rating, SubmissionRating.best);
      expect(d.evaluation!.positivePoints, 'Màu sắc hài hòa');
      expect(d.evaluation!.negativePoints, isNull);
      expect(d.evaluation!.evaluatedAt.day, 16);
      expect(d.awards.single.awardName, 'Giải Nhất');
      expect(d.awards.single.rank, 1);
    });

    test('danh sách Bài của tôi: rating null và "Best"', () {
      final list =
          (jsonDecode(_myList) as List).map((e) => SubmissionListItem.fromJson(e as Map<String, dynamic>)).toList();
      expect(list.map((e) => e.id), [3, 1]);
      expect(list[0].rating, isNull);
      expect(list[1].rating, SubmissionRating.best);
      expect(list[1].competitionStatus, CompetitionStatus.graded);
    });

    test('giải đã trao: prizeValue 2000000.00 đọc thành num', () {
      final list = (jsonDecode(_awards) as List).map((e) => AwardResult.fromJson(e as Map<String, dynamic>)).toList();
      expect(list.first.prizeValue, 2000000);
      expect(list.first.prizeValue, isA<num>());
      expect(list.last.studentName, 'Phạm Ngọc Lan');
    });

    test('my-submission: null và có id', () {
      expect(_map('{"submissionId":null}')['submissionId'] as int?, isNull);
      expect(_map('{"submissionId":3}')['submissionId'] as int?, 3);
    });
  });

  group('ApiException với ProblemDetails thật', () {
    test('409 trùng bài: có submissionId, không lẫn khóa chuẩn', () {
      final e = ApiException.from(_err(409, _map(_dup409)));
      expect(e.statusCode, 409);
      expect(e.submissionId, 3);
      expect(e.message, startsWith('Bạn đã có bài dự thi'));
      expect(e.extensions.keys, ['submissionId']);
    });

    test('409 thân dạng byte (ResponseType.bytes)', () {
      final e = ApiException.from(_err(409, utf8.encode(_dup409)));
      expect(e.submissionId, 3);
    });

    test('403 quá hạn / 400 thiếu tiêu đề / 404 ảnh dùng title tiếng Việt', () {
      expect(ApiException.from(_err(403, _map(_overdue403))).message, 'Đã quá hạn nộp bài');
      expect(ApiException.from(_err(400, _map(_missing400))).message, 'Vui lòng nhập tiêu đề');
      final e = ApiException.from(_err(404, utf8.encode(_image404)));
      expect(e.message, 'Không tìm thấy ảnh của bài dự thi');
      expect(e.statusCode, 404);
      expect(e.submissionId, isNull);
    });

    test('400 ảnh quá lớn: hiện title tiếng Việt, không hiện câu tiếng Anh trong errors', () {
      final e = ApiException.from(_err(400, _map(_oversize400)));
      expect(e.message, 'Ảnh vượt quá 5 MB');
    });
  });

  group('Query bộ lọc', () {
    test('rating gửi đúng chữ API, ngày yyyy-MM-dd, bỏ trường rỗng', () {
      final q = SubmissionFilter(
        competitionId: 1,
        q: ' Biển ',
        from: DateTime(2026, 1, 5),
        to: DateTime(2026, 12, 31),
        rating: SubmissionRating.disqualified,
      ).toQuery();
      expect(q, {'competitionId': 1, 'q': 'Biển', 'from': '2026-01-05', 'to': '2026-12-31', 'rating': 'Disqualified'});
      expect(const SubmissionFilter().toQuery(), isEmpty);
    });
  });

  test('đổi tài khoản thì danh sách bài của tài khoản cũ không còn bị giữ lại', () async {
    final storage = _MemStorage();
    final dio = Dio(BaseOptions(baseUrl: 'http://x'))
      ..httpClientAdapter = _FakeAdapter()
      ..interceptors.add(InterceptorsWrapper(onRequest: (o, h) async {
        final t = await storage.read();
        if (t != null) o.headers['Authorization'] = 'Bearer $t';
        h.next(o);
      }));
    final c = ProviderContainer(overrides: [
      dioProvider.overrideWithValue(dio),
      tokenStorageProvider.overrideWithValue(storage),
    ]);
    addTearDown(c.dispose);
    c.read(routerProvider); // router lắng nghe đăng nhập/đăng xuất
    await c.read(authControllerProvider.future); // khôi phục phiên: chưa đăng nhập

    await c.read(authControllerProvider.notifier).login('a', 'x');
    expect((await c.read(mySubmissionsProvider.future)).length, 2);

    await c.read(authControllerProvider.notifier).logout();
    await c.read(authControllerProvider.notifier).login('b', 'x');
    // Nếu cache cũ còn, sẽ vẫn trả 2 bài của tài khoản a
    expect(await c.read(mySubmissionsProvider.future), isEmpty);
  });
}
