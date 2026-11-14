import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../competitions/competitions.dart';

// ---------------- Model (khớp DTO của API) ----------------

/// Xếp loại bài dự thi, API trả dạng chữ "Best", "Better"... (F-M07).
enum SubmissionRating { best, better, good, moderate, normal, disqualified }

SubmissionRating ratingFromJson(String value) =>
    SubmissionRating.values.firstWhere((r) => r.name.toLowerCase() == value.toLowerCase());

extension SubmissionRatingView on SubmissionRating {
  /// Nhãn song ngữ (NFR-11).
  String get label => switch (this) {
        SubmissionRating.best => 'Xuất sắc nhất (BEST)',
        SubmissionRating.better => 'Tốt hơn (BETTER)',
        SubmissionRating.good => 'Tốt (GOOD)',
        SubmissionRating.moderate => 'Khá (MODERATE)',
        SubmissionRating.normal => 'Bình thường (NORMAL)',
        SubmissionRating.disqualified => 'Bị loại (DISQUALIFIED)',
      };

  /// Giá trị gửi lên API khi lọc (`rating=Best`).
  String get apiValue => name[0].toUpperCase() + name.substring(1);
}

/// Loại phần chữ đi kèm tranh (BR-19).
enum TextType { poem, quotation, story, other }

TextType textTypeFromJson(String value) =>
    TextType.values.firstWhere((t) => t.name.toLowerCase() == value.toLowerCase());

extension TextTypeView on TextType {
  String get label => switch (this) {
        TextType.poem => 'Thơ',
        TextType.quotation => 'Trích dẫn',
        TextType.story => 'Câu chuyện',
        TextType.other => 'Khác',
      };

  /// Giá trị gửi lên API (`Poem`, `Quotation`...).
  String get apiValue => name[0].toUpperCase() + name.substring(1);
}

/// Một dòng trong danh sách "Bài của tôi".
class SubmissionListItem {
  const SubmissionListItem({
    required this.id,
    required this.competitionId,
    required this.competitionTitle,
    required this.competitionStatus,
    required this.title,
    required this.submittedAt,
    required this.updatedAt,
    required this.canEdit,
    required this.rating,
  });

  final int id;
  final int competitionId;
  final String competitionTitle;
  final CompetitionStatus competitionStatus;
  final String title;
  final DateTime submittedAt;
  final DateTime? updatedAt;
  final bool canEdit;
  final SubmissionRating? rating;

  factory SubmissionListItem.fromJson(Map<String, dynamic> json) => SubmissionListItem(
        id: json['id'] as int,
        competitionId: json['competitionId'] as int,
        competitionTitle: json['competitionTitle'] as String,
        competitionStatus: statusFromJson(json['competitionStatus'] as String),
        title: json['title'] as String,
        submittedAt: DateTime.parse(json['submittedAt'] as String),
        updatedAt: _dateOrNull(json['updatedAt']),
        canEdit: json['canEdit'] as bool,
        rating: json['rating'] == null ? null : ratingFromJson(json['rating'] as String),
      );
}

/// Nhận xét của giáo viên cho một bài (F-M07).
class SubmissionEvaluation {
  const SubmissionEvaluation({
    required this.rating,
    required this.positivePoints,
    required this.negativePoints,
    required this.improvementAreas,
    required this.evaluatedAt,
  });

  final SubmissionRating rating;
  final String? positivePoints;
  final String? negativePoints;
  final String? improvementAreas;
  final DateTime evaluatedAt;

  factory SubmissionEvaluation.fromJson(Map<String, dynamic> json) => SubmissionEvaluation(
        rating: ratingFromJson(json['rating'] as String),
        positivePoints: json['positivePoints'] as String?,
        negativePoints: json['negativePoints'] as String?,
        improvementAreas: json['improvementAreas'] as String?,
        evaluatedAt: DateTime.parse(json['evaluatedAt'] as String),
      );
}

/// Giải đã trao kèm trong chi tiết bài.
class SubmissionAward {
  const SubmissionAward({required this.awardName, required this.rank});

  final String awardName;
  final int rank;

  factory SubmissionAward.fromJson(Map<String, dynamic> json) =>
      SubmissionAward(awardName: json['awardName'] as String, rank: json['rank'] as int);
}

/// Chi tiết một bài dự thi.
class SubmissionDetail {
  const SubmissionDetail({
    required this.id,
    required this.competitionId,
    required this.competitionTitle,
    required this.competitionStatus,
    required this.title,
    required this.description,
    required this.textType,
    required this.textContent,
    required this.publicConsent,
    required this.originalFileName,
    required this.submittedAt,
    required this.updatedAt,
    required this.canEdit,
    required this.evaluation,
    required this.awards,
  });

  final int id;
  final int competitionId;
  final String competitionTitle;
  final CompetitionStatus competitionStatus;
  final String title;
  final String description;
  final TextType textType;
  final String textContent;
  final bool publicConsent;
  final String originalFileName;
  final DateTime submittedAt;
  final DateTime? updatedAt;
  final bool canEdit;
  final SubmissionEvaluation? evaluation;
  final List<SubmissionAward> awards;

  factory SubmissionDetail.fromJson(Map<String, dynamic> json) => SubmissionDetail(
        id: json['id'] as int,
        competitionId: json['competitionId'] as int,
        competitionTitle: json['competitionTitle'] as String,
        competitionStatus: statusFromJson(json['competitionStatus'] as String),
        title: json['title'] as String,
        description: json['description'] as String,
        textType: textTypeFromJson(json['textType'] as String),
        textContent: json['textContent'] as String,
        publicConsent: json['publicConsent'] as bool,
        originalFileName: json['originalFileName'] as String,
        submittedAt: DateTime.parse(json['submittedAt'] as String),
        updatedAt: _dateOrNull(json['updatedAt']),
        canEdit: json['canEdit'] as bool,
        // Chưa chấm thì evaluation = null
        evaluation: json['evaluation'] == null
            ? null
            : SubmissionEvaluation.fromJson(json['evaluation'] as Map<String, dynamic>),
        // Thiếu `awards` coi như chưa có giải
        awards: ((json['awards'] as List?) ?? const [])
            .map((e) => SubmissionAward.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Giải đã trao của một cuộc thi (API công khai, chỉ có khi đã công bố kết quả).
class AwardResult {
  const AwardResult({
    required this.awardName,
    required this.rank,
    required this.studentName,
    required this.paintingTitle,
    required this.prizeValue,
  });

  final String awardName;
  final int rank;
  final String studentName;
  final String paintingTitle;
  final num prizeValue;

  factory AwardResult.fromJson(Map<String, dynamic> json) => AwardResult(
        awardName: json['awardName'] as String,
        rank: json['rank'] as int,
        studentName: json['studentName'] as String,
        paintingTitle: json['paintingTitle'] as String,
        prizeValue: json['prizeValue'] as num,
      );
}

DateTime? _dateOrNull(Object? v) => v == null ? null : DateTime.parse(v as String);

// ---------------- Hàm thuần: kiểm tra ảnh & nhãn hạn nộp ----------------

/// Dung lượng ảnh tối đa 5 MB (BR-17).
const maxImageBytes = 5 * 1024 * 1024;

/// Kiểm tra ảnh phía client (BR-16, BR-17). Trả về thông báo lỗi, hoặc null nếu hợp lệ.
/// Máy chủ vẫn là nơi quyết định cuối cùng (kiểm tra cả magic bytes).
String? validateImageFile(String fileName, int sizeBytes) {
  final lower = fileName.toLowerCase();
  if (!lower.endsWith('.jpg') && !lower.endsWith('.jpeg')) return 'Chỉ chấp nhận ảnh JPEG';
  if (sizeBytes <= 0) return 'File ảnh rỗng, vui lòng chọn ảnh khác';
  if (sizeBytes > maxImageBytes) return 'Ảnh vượt quá 5 MB';
  return null;
}

/// Cạnh ngắn tối thiểu khuyến nghị của ảnh (BR-17), đơn vị pixel.
const minRecommendedShortSide = 600;

/// Cảnh báo ảnh nhỏ (BR-17): chỉ nhắc, không chặn nộp. Trả null nếu cạnh ngắn đủ lớn.
String? smallImageWarning(int width, int height) {
  final shortSide = width < height ? width : height;
  if (shortSide >= minRecommendedShortSide) return null;
  return 'Ảnh khá nhỏ ($width×$height px). Nên dùng ảnh có cạnh ngắn từ $minRecommendedShortSide px trở lên '
      'để tranh hiển thị rõ nét. Bạn vẫn có thể nộp ảnh này.';
}

/// Nhãn hạn nộp (F-M06-10): cuộc thi đang diễn ra → "Còn X ngày"; đã đóng/chấm → "Đã hết hạn nộp bài".
/// Cuộc thi chưa bắt đầu thì không có nhãn.
String? deadlineLabel(CompetitionStatus status, int daysLeft) {
  switch (status) {
    case CompetitionStatus.ongoing:
      return daysLeft <= 0 ? 'Hôm nay là hạn nộp cuối' : 'Còn $daysLeft ngày';
    case CompetitionStatus.closed:
    case CompetitionStatus.graded:
      return 'Đã hết hạn nộp bài';
    case CompetitionStatus.upcoming:
      return null;
  }
}

// ---------------- Bộ lọc danh sách ----------------

/// Điều kiện lọc "Bài của tôi" (F-M06-05).
class SubmissionFilter {
  const SubmissionFilter({this.competitionId, this.q = '', this.from, this.to, this.rating});

  final int? competitionId;
  final String q;
  final DateTime? from;
  final DateTime? to;
  final SubmissionRating? rating;

  /// Có điều kiện lọc nào đang bật không (để chọn thông báo "không có bài" cho đúng).
  bool get isActive => competitionId != null || q.isNotEmpty || from != null || to != null || rating != null;

  /// Tham số query gửi lên API; ngày dạng yyyy-MM-dd.
  Map<String, dynamic> toQuery() => {
        if (competitionId != null) 'competitionId': competitionId,
        if (q.trim().isNotEmpty) 'q': q.trim(),
        if (from != null) 'from': _ymd(from!),
        if (to != null) 'to': _ymd(to!),
        if (rating != null) 'rating': rating!.apiValue,
      };

  SubmissionFilter copyWith({
    int? Function()? competitionId,
    String? q,
    DateTime? Function()? from,
    DateTime? Function()? to,
    SubmissionRating? Function()? rating,
  }) =>
      SubmissionFilter(
        competitionId: competitionId != null ? competitionId() : this.competitionId,
        q: q ?? this.q,
        from: from != null ? from() : this.from,
        to: to != null ? to() : this.to,
        rating: rating != null ? rating() : this.rating,
      );

  @override
  bool operator ==(Object other) =>
      other is SubmissionFilter &&
      other.competitionId == competitionId &&
      other.q == q &&
      other.from == from &&
      other.to == to &&
      other.rating == rating;

  @override
  int get hashCode => Object.hash(competitionId, q, from, to, rating);
}

String _ymd(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

/// Chuyển query của bộ lọc thành chuỗi ngày chuẩn (xuất ra để test).
String formatQueryDate(DateTime d) => _ymd(d);

// ---------------- Repository: gọi API ----------------

/// Dữ liệu một lần nộp/sửa bài gửi lên server (multipart).
class SubmissionInput {
  const SubmissionInput({
    required this.title,
    required this.description,
    required this.textType,
    required this.textContent,
    required this.publicConsent,
    this.imageBytes,
    this.imageName,
  });

  final String title;
  final String description;
  final TextType textType;
  final String textContent;
  final bool publicConsent;
  final Uint8List? imageBytes;
  final String? imageName;
}

/// Thời gian chờ dài hơn cho lời gọi chuyển ảnh (tối đa 5 MB) trên đường truyền chậm.
/// Trên web, Dio đặt xhr.timeout = connectTimeout + receiveTimeout cho CẢ request (gồm lúc tải lên),
/// nên mặc định 10 + 20 giây sẽ cắt ngang ảnh 5 MB khi mạng dưới ~1,4 Mbps.
final _transferOptions = Options(receiveTimeout: const Duration(minutes: 2));

class SubmissionRepository {
  SubmissionRepository(this._dio);

  final Dio _dio;

  /// Tạo FormData đúng tên trường theo hợp đồng API; ảnh chỉ đính kèm khi có.
  Future<FormData> _form(SubmissionInput input) async => FormData.fromMap({
        'title': input.title,
        'description': input.description,
        'textType': input.textType.apiValue,
        'textContent': input.textContent,
        'publicConsent': input.publicConsent ? 'true' : 'false',
        if (input.imageBytes != null)
          'image': MultipartFile.fromBytes(
            input.imageBytes!,
            filename: input.imageName ?? 'image.jpg',
            contentType: DioMediaType('image', 'jpeg'),
          ),
      });

  /// POST /api/competitions/{id}/submissions → 201 chi tiết bài (UC-03).
  Future<SubmissionDetail> create(int competitionId, SubmissionInput input) => guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>(
          '/api/competitions/$competitionId/submissions',
          data: await _form(input),
          options: _transferOptions,
        );
        return SubmissionDetail.fromJson(res.data!);
      });

  /// PUT /api/submissions/{id} → 200 chi tiết bài (UC-04); ảnh là tùy chọn.
  Future<SubmissionDetail> update(int id, SubmissionInput input) => guardApi(() async {
        final res = await _dio.put<Map<String, dynamic>>(
          '/api/submissions/$id',
          data: await _form(input),
          options: _transferOptions,
        );
        return SubmissionDetail.fromJson(res.data!);
      });

  /// DELETE /api/submissions/{id} → 204.
  Future<void> delete(int id) => guardApi(() async {
        await _dio.delete<void>('/api/submissions/$id');
      });

  /// GET /api/submissions/{id}
  Future<SubmissionDetail> detail(int id) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/submissions/$id');
        return SubmissionDetail.fromJson(res.data!);
      });

  /// GET /api/my/submissions với bộ lọc.
  Future<List<SubmissionListItem>> mine(SubmissionFilter filter) => guardApi(() async {
        final res = await _dio.get<List<dynamic>>('/api/my/submissions', queryParameters: filter.toQuery());
        return res.data!.map((e) => SubmissionListItem.fromJson(e as Map<String, dynamic>)).toList();
      });

  /// GET /api/submissions/{id}/image → byte JPEG (cần JWT nên không dùng Image.network).
  Future<Uint8List> imageBytes(int id, {bool download = false}) => guardApi(() async {
        final res = await _dio.get<List<int>>(
          '/api/submissions/$id/image',
          queryParameters: {'download': download},
          options: Options(responseType: ResponseType.bytes, receiveTimeout: _transferOptions.receiveTimeout),
        );
        return Uint8List.fromList(res.data!);
      });

  /// GET /api/competitions/{id}/my-submission → id bài đã nộp hoặc null.
  Future<int?> mySubmissionId(int competitionId) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/competitions/$competitionId/my-submission');
        return res.data!['submissionId'] as int?;
      });

  /// GET /api/competitions/{id}/awards (công khai) → giải đã trao.
  Future<List<AwardResult>> awards(int competitionId) => guardApi(() async {
        final res = await _dio.get<List<dynamic>>('/api/competitions/$competitionId/awards');
        return res.data!.map((e) => AwardResult.fromJson(e as Map<String, dynamic>)).toList();
      });
}

// ---------------- Provider ----------------

final submissionRepositoryProvider =
    Provider<SubmissionRepository>((ref) => SubmissionRepository(ref.watch(dioProvider)));

/// Bộ lọc đang chọn ở tab "Bài của tôi".
class SubmissionFilterNotifier extends Notifier<SubmissionFilter> {
  @override
  SubmissionFilter build() => const SubmissionFilter();

  void set(SubmissionFilter value) => state = value;
}

final submissionFilterProvider =
    NotifierProvider<SubmissionFilterNotifier, SubmissionFilter>(SubmissionFilterNotifier.new);

/// Danh sách bài của tôi theo bộ lọc hiện tại.
final mySubmissionsProvider = FutureProvider<List<SubmissionListItem>>((ref) {
  final filter = ref.watch(submissionFilterProvider);
  return ref.watch(submissionRepositoryProvider).mine(filter);
});

/// Toàn bộ bài của tôi, không lọc (dùng cho tab "Kết quả & giải").
final allMySubmissionsProvider = FutureProvider<List<SubmissionListItem>>(
  (ref) => ref.watch(submissionRepositoryProvider).mine(const SubmissionFilter()),
);

/// Chi tiết một bài.
final submissionDetailProvider = FutureProvider.family<SubmissionDetail, int>(
  (ref, id) => ref.watch(submissionRepositoryProvider).detail(id),
);

/// Ảnh của bài, tải bằng Dio (có JWT) và được Riverpod giữ lại để không tải lại mỗi lần mở.
final submissionImageProvider = FutureProvider.family<Uint8List, int>(
  (ref, id) => ref.watch(submissionRepositoryProvider).imageBytes(id),
);

/// Bài đã nộp của tôi trong một cuộc thi (null nếu chưa nộp).
final mySubmissionIdProvider = FutureProvider.family<int?, int>(
  (ref, competitionId) => ref.watch(submissionRepositoryProvider).mySubmissionId(competitionId),
);

/// Giải đã trao của một cuộc thi.
final competitionAwardsProvider = FutureProvider.family<List<AwardResult>, int>(
  (ref, competitionId) => ref.watch(submissionRepositoryProvider).awards(competitionId),
);

/// Danh sách cuộc thi (cho ô chọn cuộc thi ở bộ lọc).
final competitionListProvider = FutureProvider<List<CompetitionSummary>>((ref) {
  final dio = ref.watch(dioProvider);
  return guardApi(() async {
    final res = await dio.get<List<dynamic>>('/api/competitions');
    return res.data!.map((e) => CompetitionSummary.fromJson(e as Map<String, dynamic>)).toList();
  });
});

/// Làm mới mọi dữ liệu phụ thuộc bài dự thi sau khi nộp/sửa/xóa (danh sách, số bài ở trang chủ...).
void refreshSubmissionData(WidgetRef ref, {int? submissionId, int? competitionId}) {
  ref.invalidate(mySubmissionsProvider);
  ref.invalidate(allMySubmissionsProvider);
  ref.invalidate(homeProvider);
  if (submissionId != null) {
    ref.invalidate(submissionDetailProvider(submissionId));
    ref.invalidate(submissionImageProvider(submissionId));
  }
  if (competitionId != null) {
    ref.invalidate(mySubmissionIdProvider(competitionId));
    ref.invalidate(competitionDetailProvider(competitionId));
  }
}

/// Xóa mọi dữ liệu theo tài khoản đang được Riverpod giữ lại (danh sách bài, chi tiết, ảnh, trang chủ...).
/// Gọi khi người dùng đăng nhập/đăng xuất để tài khoản sau không thấy dữ liệu của tài khoản trước.
void resetUserScopedData(Ref ref) {
  ref.invalidate(homeProvider);
  ref.invalidate(competitionDetailProvider);
  ref.invalidate(mySubmissionsProvider);
  ref.invalidate(allMySubmissionsProvider);
  ref.invalidate(submissionDetailProvider);
  ref.invalidate(submissionImageProvider);
  ref.invalidate(mySubmissionIdProvider);
  ref.invalidate(competitionAwardsProvider);
  ref.invalidate(submissionFilterProvider);
}
