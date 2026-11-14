import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../competitions/competitions.dart';
import '../submissions/submissions.dart';

// ---------------- Model (khớp DTO của API chấm bài) ----------------

/// Cuộc thi trong màn "Chấm bài" kèm tiến độ chấm.
class GradingCompetition {
  const GradingCompetition({
    required this.id,
    required this.title,
    required this.endDate,
    required this.status,
    required this.submissionCount,
    required this.evaluatedCount,
  });

  final int id;
  final String title;
  final DateTime endDate;
  final CompetitionStatus status;
  final int submissionCount;
  final int evaluatedCount;

  factory GradingCompetition.fromJson(Map<String, dynamic> json) => GradingCompetition(
        id: json['id'] as int,
        title: json['title'] as String,
        endDate: DateTime.parse(json['endDate'] as String),
        status: statusFromJson(json['status'] as String),
        submissionCount: json['submissionCount'] as int,
        evaluatedCount: json['evaluatedCount'] as int,
      );
}

/// Một dòng trong danh sách bài cần chấm; [rating] null = chưa chấm.
class GradingItem {
  const GradingItem({
    required this.id,
    required this.title,
    required this.studentName,
    required this.studentCode,
    required this.submittedAt,
    required this.rating,
  });

  final int id;
  final String title;
  final String studentName;
  final String studentCode;
  final DateTime submittedAt;
  final SubmissionRating? rating;

  bool get isEvaluated => rating != null;

  factory GradingItem.fromJson(Map<String, dynamic> json) => GradingItem(
        id: json['id'] as int,
        title: json['title'] as String,
        studentName: json['studentName'] as String,
        studentCode: json['studentCode'] as String,
        submittedAt: DateTime.parse(json['submittedAt'] as String),
        rating: json['rating'] == null ? null : ratingFromJson(json['rating'] as String),
      );
}

/// Danh sách bài của một cuộc thi; [total]/[evaluated] là tiến độ cả cuộc thi, không phụ thuộc bộ lọc.
class GradingList {
  const GradingList({
    required this.competitionId,
    required this.competitionTitle,
    required this.status,
    required this.total,
    required this.evaluated,
    required this.items,
  });

  final int competitionId;
  final String competitionTitle;
  final CompetitionStatus status;
  final int total;
  final int evaluated;
  final List<GradingItem> items;

  factory GradingList.fromJson(Map<String, dynamic> json) => GradingList(
        competitionId: json['competitionId'] as int,
        competitionTitle: json['competitionTitle'] as String,
        status: statusFromJson(json['status'] as String),
        total: json['total'] as int,
        evaluated: json['evaluated'] as int,
        items: (json['items'] as List).map((e) => GradingItem.fromJson(e as Map<String, dynamic>)).toList(),
      );
}

/// Đánh giá hiện có; [rowVersion] gửi lại khi sửa/xóa để server phát hiện xung đột (BR-52).
class StaffEvaluation {
  const StaffEvaluation({
    required this.rating,
    required this.positivePoints,
    required this.negativePoints,
    required this.improvementAreas,
    required this.evaluatedByName,
    required this.evaluatedAt,
    required this.rowVersion,
  });

  final SubmissionRating rating;
  final String? positivePoints;
  final String? negativePoints;
  final String? improvementAreas;
  final String evaluatedByName;
  final DateTime evaluatedAt;
  final String rowVersion;

  factory StaffEvaluation.fromJson(Map<String, dynamic> json) => StaffEvaluation(
        rating: ratingFromJson(json['rating'] as String),
        positivePoints: json['positivePoints'] as String?,
        negativePoints: json['negativePoints'] as String?,
        improvementAreas: json['improvementAreas'] as String?,
        evaluatedByName: json['evaluatedByName'] as String,
        evaluatedAt: DateTime.parse(json['evaluatedAt'] as String),
        rowVersion: json['rowVersion'] as String,
      );
}

/// Chi tiết bài để chấm.
class GradingDetail {
  const GradingDetail({
    required this.id,
    required this.competitionId,
    required this.competitionTitle,
    required this.competitionStatus,
    required this.title,
    required this.description,
    required this.textType,
    required this.textContent,
    required this.studentName,
    required this.studentCode,
    required this.submittedAt,
    required this.updatedAt,
    required this.canEvaluate,
    required this.canDeleteEvaluation,
    required this.lockReason,
    required this.evaluation,
    required this.nextUnevaluatedId,
    required this.total,
    required this.evaluated,
  });

  final int id;
  final int competitionId;
  final String competitionTitle;
  final CompetitionStatus competitionStatus;
  final String title;
  final String description;
  final TextType textType;
  final String textContent;
  final String studentName;
  final String studentCode;
  final DateTime submittedAt;
  final DateTime? updatedAt;
  final bool canEvaluate;
  final bool canDeleteEvaluation;

  /// Lý do không được chấm/sửa (vd "Chưa hết hạn nộp"); null khi được phép.
  final String? lockReason;
  final StaffEvaluation? evaluation;

  /// Bài chưa chấm kế tiếp để chuyển sang sau khi lưu (UC-05 bước 5); null nếu đã chấm hết.
  final int? nextUnevaluatedId;
  final int total;
  final int evaluated;

  factory GradingDetail.fromJson(Map<String, dynamic> json) => GradingDetail(
        id: json['id'] as int,
        competitionId: json['competitionId'] as int,
        competitionTitle: json['competitionTitle'] as String,
        competitionStatus: statusFromJson(json['competitionStatus'] as String),
        title: json['title'] as String,
        description: json['description'] as String,
        textType: textTypeFromJson(json['textType'] as String),
        textContent: json['textContent'] as String,
        studentName: json['studentName'] as String,
        studentCode: json['studentCode'] as String,
        submittedAt: DateTime.parse(json['submittedAt'] as String),
        updatedAt: json['updatedAt'] == null ? null : DateTime.parse(json['updatedAt'] as String),
        canEvaluate: json['canEvaluate'] as bool,
        canDeleteEvaluation: json['canDeleteEvaluation'] as bool,
        lockReason: json['lockReason'] as String?,
        evaluation: json['evaluation'] == null
            ? null
            : StaffEvaluation.fromJson(json['evaluation'] as Map<String, dynamic>),
        nextUnevaluatedId: json['nextUnevaluatedId'] as int?,
        total: json['total'] as int,
        evaluated: json['evaluated'] as int,
      );
}

/// Dữ liệu chấm bài gửi lên; [rowVersion] bắt buộc khi bài đã có đánh giá.
class EvaluationInput {
  const EvaluationInput({
    required this.rating,
    required this.positivePoints,
    required this.negativePoints,
    required this.improvementAreas,
    this.rowVersion,
  });

  final SubmissionRating rating;
  final String positivePoints;
  final String negativePoints;
  final String improvementAreas;
  final String? rowVersion;

  Map<String, dynamic> toJson() => {
        'rating': rating.apiValue,
        'positivePoints': positivePoints,
        'negativePoints': negativePoints,
        'improvementAreas': improvementAreas,
        if (rowVersion != null) 'rowVersion': rowVersion,
      };
}

// ---------------- Hàm thuần ----------------

/// Kiểm tra phía client theo BR-23; server vẫn là nơi quyết định cuối cùng.
/// DISQUALIFIED bắt buộc lý do (ở ô "điểm yếu"); xếp loại khác bắt buộc điểm mạnh.
String? validateEvaluation(SubmissionRating? rating, String positive, String negative) {
  if (rating == null) return 'Vui lòng chọn xếp loại';
  if (rating == SubmissionRating.disqualified) {
    return negative.trim().isEmpty ? 'Xếp loại DISQUALIFIED bắt buộc nhập lý do' : null;
  }
  return positive.trim().isEmpty ? 'Vui lòng nhập điểm mạnh' : null;
}

/// Tiến độ dạng "x/y" (UC-05 bước 5).
String progressLabel(int evaluated, int total) => '$evaluated/$total';

/// Bộ lọc danh sách bài cần chấm. [graded]: null = tất cả, false = chưa chấm, true = đã chấm.
class GradingFilter {
  const GradingFilter({this.graded, this.rating, this.q = ''});

  final bool? graded;
  final SubmissionRating? rating;
  final String q;

  Map<String, dynamic> toQuery() => {
        if (graded != null) 'graded': graded,
        if (rating != null) 'rating': rating!.apiValue,
        if (q.trim().isNotEmpty) 'q': q.trim(),
      };

  @override
  bool operator ==(Object other) =>
      other is GradingFilter && other.graded == graded && other.rating == rating && other.q == q;

  @override
  int get hashCode => Object.hash(graded, rating, q);
}

// ---------------- Repository ----------------

class GradingRepository {
  GradingRepository(this._dio);

  final Dio _dio;

  /// GET /api/staff/competitions
  Future<List<GradingCompetition>> competitions() => guardApi(() async {
        final res = await _dio.get<List<dynamic>>('/api/staff/competitions');
        return res.data!.map((e) => GradingCompetition.fromJson(e as Map<String, dynamic>)).toList();
      });

  /// GET /api/staff/competitions/{id}/submissions
  Future<GradingList> list(int competitionId, GradingFilter filter) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>(
          '/api/staff/competitions/$competitionId/submissions',
          queryParameters: filter.toQuery(),
        );
        return GradingList.fromJson(res.data!);
      });

  /// GET /api/staff/submissions/{id}
  Future<GradingDetail> detail(int id) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/staff/submissions/$id');
        return GradingDetail.fromJson(res.data!);
      });

  /// PUT /api/staff/submissions/{id}/evaluation → chi tiết mới (tạo hoặc sửa).
  Future<GradingDetail> save(int id, EvaluationInput input) => guardApi(() async {
        final res = await _dio.put<Map<String, dynamic>>('/api/staff/submissions/$id/evaluation', data: input.toJson());
        return GradingDetail.fromJson(res.data!);
      });

  /// DELETE /api/staff/submissions/{id}/evaluation?rowVersion=...
  Future<void> deleteEvaluation(int id, String rowVersion) => guardApi(() async {
        await _dio.delete<void>('/api/staff/submissions/$id/evaluation', queryParameters: {'rowVersion': rowVersion});
      });
}

// ---------------- Provider ----------------

final gradingRepositoryProvider = Provider<GradingRepository>((ref) => GradingRepository(ref.watch(dioProvider)));

/// Cuộc thi + tiến độ chấm.
final gradingCompetitionsProvider = FutureProvider<List<GradingCompetition>>(
  (ref) => ref.watch(gradingRepositoryProvider).competitions(),
);

/// Danh sách bài theo cuộc thi và bộ lọc (record có so sánh theo giá trị nên làm khóa family được).
final gradingListProvider = FutureProvider.family<GradingList, ({int competitionId, GradingFilter filter})>(
  (ref, key) => ref.watch(gradingRepositoryProvider).list(key.competitionId, key.filter),
);

/// Chi tiết một bài để chấm.
final gradingDetailProvider = FutureProvider.family<GradingDetail, int>(
  (ref, id) => ref.watch(gradingRepositoryProvider).detail(id),
);

/// Làm mới dữ liệu chấm bài sau khi lưu/xóa đánh giá.
void refreshGradingData(WidgetRef ref, {int? submissionId}) {
  ref.invalidate(gradingCompetitionsProvider);
  ref.invalidate(gradingListProvider);
  if (submissionId != null) ref.invalidate(gradingDetailProvider(submissionId));
}

/// Xóa dữ liệu chấm bài đang giữ khi đổi tài khoản.
void resetGradingData(Ref ref) {
  ref.invalidate(gradingCompetitionsProvider);
  ref.invalidate(gradingListProvider);
  ref.invalidate(gradingDetailProvider);
}
