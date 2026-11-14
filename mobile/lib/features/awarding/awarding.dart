import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../competitions/competitions.dart';
import '../grading/grading.dart';
import '../submissions/submissions.dart';

// ---------------- Model (khớp DTO của API trao giải: AwardingDtos.cs) ----------------

/// Trạng thái một giải đã trao (BR-33: thu hồi sau công bố = revoked, không xóa).
enum IssuedStatus { active, revoked }

IssuedStatus issuedStatusFromJson(String value) =>
    IssuedStatus.values.firstWhere((s) => s.name.toLowerCase() == value.toLowerCase());

/// Cuộc thi trong tab "Trao giải": đã hết hạn nộp hoặc đã công bố.
class AwardingCompetition {
  const AwardingCompetition({
    required this.id,
    required this.title,
    required this.endDate,
    required this.status,
    required this.submissionCount,
    required this.evaluatedCount,
    required this.totalSlots,
    required this.issuedSlots,
    required this.adjustedAt,
  });

  final int id;
  final String title;
  final DateTime endDate;
  final CompetitionStatus status;
  final int submissionCount;
  final int evaluatedCount;

  /// Tổng số suất giải định nghĩa và số suất đã trao (đang hiệu lực).
  final int totalSlots;
  final int issuedSlots;
  final DateTime? adjustedAt;

  factory AwardingCompetition.fromJson(Map<String, dynamic> json) => AwardingCompetition(
        id: json['id'] as int,
        title: json['title'] as String,
        endDate: DateTime.parse(json['endDate'] as String),
        status: statusFromJson(json['status'] as String),
        submissionCount: json['submissionCount'] as int,
        evaluatedCount: json['evaluatedCount'] as int,
        totalSlots: json['totalSlots'] as int,
        issuedSlots: json['issuedSlots'] as int,
        adjustedAt: _dateOrNull(json['adjustedAt']),
      );
}

/// Một giải đã trao; [rowVersion] gửi lại khi sửa/xóa/thu hồi/phát thưởng (BR-52).
class IssuedAward {
  const IssuedAward({
    required this.id,
    required this.submissionId,
    required this.submissionTitle,
    required this.studentName,
    required this.studentCode,
    required this.rating,
    required this.issuedDate,
    required this.remarks,
    required this.prizeValue,
    required this.status,
    required this.revokedReason,
    required this.isDelivered,
    required this.deliveredAt,
    required this.rowVersion,
  });

  final int id;
  final int submissionId;
  final String submissionTitle;
  final String studentName;
  final String studentCode;
  final SubmissionRating? rating;
  final DateTime issuedDate;
  final String? remarks;
  final num prizeValue;
  final IssuedStatus status;
  final String? revokedReason;
  final bool isDelivered;
  final DateTime? deliveredAt;
  final String rowVersion;

  bool get isActive => status == IssuedStatus.active;

  factory IssuedAward.fromJson(Map<String, dynamic> json) => IssuedAward(
        id: json['id'] as int,
        submissionId: json['submissionId'] as int,
        submissionTitle: json['submissionTitle'] as String,
        studentName: json['studentName'] as String,
        studentCode: json['studentCode'] as String,
        rating: json['rating'] == null ? null : ratingFromJson(json['rating'] as String),
        issuedDate: DateTime.parse(json['issuedDate'] as String),
        remarks: json['remarks'] as String?,
        prizeValue: json['prizeValue'] as num,
        status: issuedStatusFromJson(json['status'] as String),
        revokedReason: json['revokedReason'] as String?,
        isDelivered: json['isDelivered'] as bool,
        deliveredAt: _dateOrNull(json['deliveredAt']),
        rowVersion: json['rowVersion'] as String,
      );
}

/// Giải định nghĩa kèm số suất đã trao / còn trống và các giải đã trao.
class AwardSlot {
  const AwardSlot({
    required this.id,
    required this.name,
    required this.rank,
    required this.prizeValue,
    required this.quantity,
    required this.description,
    required this.activeCount,
    required this.remaining,
    required this.issued,
  });

  final int id;
  final String name;
  final int rank;
  final num prizeValue;
  final int quantity;
  final String? description;
  final int activeCount;
  final int remaining;
  final List<IssuedAward> issued;

  factory AwardSlot.fromJson(Map<String, dynamic> json) => AwardSlot(
        id: json['id'] as int,
        name: json['name'] as String,
        rank: json['rank'] as int,
        prizeValue: json['prizeValue'] as num,
        quantity: json['quantity'] as int,
        description: json['description'] as String?,
        activeCount: json['activeCount'] as int,
        remaining: json['remaining'] as int,
        issued: (json['issued'] as List).map((e) => IssuedAward.fromJson(e as Map<String, dynamic>)).toList(),
      );
}

/// Chi tiết trao giải của một cuộc thi kèm checklist công bố (BR-32).
class AwardingDetail {
  const AwardingDetail({
    required this.id,
    required this.title,
    required this.status,
    required this.submissionCount,
    required this.evaluatedCount,
    required this.pendingEvaluations,
    required this.emptySlots,
    required this.canAward,
    required this.canPublish,
    required this.publishBlockReason,
    required this.isPublished,
    required this.publishedAt,
    required this.adjustedAt,
    required this.awards,
  });

  final int id;
  final String title;
  final CompetitionStatus status;
  final int submissionCount;
  final int evaluatedCount;

  /// Số bài chưa chấm; phải bằng 0 mới công bố được (BR-08).
  final int pendingEvaluations;

  /// Tổng số suất giải chưa trao; phải được Staff xác nhận "để trống" khi công bố (BR-32).
  final int emptySlots;
  final bool canAward;
  final bool canPublish;

  /// Lý do chưa công bố được (vd "Còn 2 bài chưa chấm"); null khi được phép.
  final String? publishBlockReason;
  final bool isPublished;
  final DateTime? publishedAt;
  final DateTime? adjustedAt;
  final List<AwardSlot> awards;

  factory AwardingDetail.fromJson(Map<String, dynamic> json) => AwardingDetail(
        id: json['id'] as int,
        title: json['title'] as String,
        status: statusFromJson(json['status'] as String),
        submissionCount: json['submissionCount'] as int,
        evaluatedCount: json['evaluatedCount'] as int,
        pendingEvaluations: json['pendingEvaluations'] as int,
        emptySlots: json['emptySlots'] as int,
        canAward: json['canAward'] as bool,
        canPublish: json['canPublish'] as bool,
        publishBlockReason: json['publishBlockReason'] as String?,
        isPublished: json['isPublished'] as bool,
        publishedAt: _dateOrNull(json['publishedAt']),
        adjustedAt: _dateOrNull(json['adjustedAt']),
        awards: (json['awards'] as List).map((e) => AwardSlot.fromJson(e as Map<String, dynamic>)).toList(),
      );
}

/// Bài đủ điều kiện nhận giải (BR-30).
class AwardCandidate {
  const AwardCandidate({
    required this.submissionId,
    required this.title,
    required this.studentName,
    required this.studentCode,
    required this.rating,
  });

  final int submissionId;
  final String title;
  final String studentName;
  final String studentCode;
  final SubmissionRating rating;

  factory AwardCandidate.fromJson(Map<String, dynamic> json) => AwardCandidate(
        submissionId: json['submissionId'] as int,
        title: json['title'] as String,
        studentName: json['studentName'] as String,
        studentCode: json['studentCode'] as String,
        rating: ratingFromJson(json['rating'] as String),
      );
}

DateTime? _dateOrNull(Object? value) => value == null ? null : DateTime.parse(value as String);

// ---------------- Hàm thuần ----------------

/// Nhãn tiến độ trao giải dạng "đã trao x/y suất".
String slotsLabel(int issued, int total) => '$issued/$total';

/// Kiểm tra lý do bắt buộc khi điều chỉnh sau công bố (BR-34); null nếu hợp lệ. Server vẫn là nơi quyết định cuối.
String? validateReason(String reason) => reason.trim().isEmpty ? 'Vui lòng nhập lý do' : null;

/// Thông báo nhắc xác nhận để trống khi công bố (UC-06 bước 5).
String emptySlotsMessage(int emptySlots) => 'Còn $emptySlots suất giải chưa trao. Tick xác nhận để trống các suất này.';

// ---------------- Repository ----------------

class AwardingRepository {
  AwardingRepository(this._dio);

  final Dio _dio;

  static const _base = '/api/staff/awarding';

  /// GET /api/staff/awarding/competitions
  Future<List<AwardingCompetition>> competitions() => guardApi(() async {
        final res = await _dio.get<List<dynamic>>('$_base/competitions');
        return res.data!.map((e) => AwardingCompetition.fromJson(e as Map<String, dynamic>)).toList();
      });

  /// GET /api/staff/awarding/competitions/{id}
  Future<AwardingDetail> detail(int id) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('$_base/competitions/$id');
        return AwardingDetail.fromJson(res.data!);
      });

  /// GET /api/staff/awarding/competitions/{id}/candidates
  Future<List<AwardCandidate>> candidates(int id) => guardApi(() async {
        final res = await _dio.get<List<dynamic>>('$_base/competitions/$id/candidates');
        return res.data!.map((e) => AwardCandidate.fromJson(e as Map<String, dynamic>)).toList();
      });

  /// POST .../awards: trao giải; [reason] bắt buộc khi cuộc thi đã công bố (trao bổ sung).
  Future<AwardingDetail> issue(int competitionId,
          {required int awardId, required int submissionId, String? remarks, String? reason}) =>
      guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('$_base/competitions/$competitionId/awards', data: {
          'competitionAwardId': awardId,
          'submissionId': submissionId,
          'remarks': remarks,
          'reason': reason,
        });
        return AwardingDetail.fromJson(res.data!);
      });

  /// PUT .../awards/{id}: đổi bài/giải/ghi chú khi chưa công bố.
  Future<AwardingDetail> update(IssuedAward award,
          {required int awardId, required int submissionId, String? remarks}) =>
      guardApi(() async {
        final res = await _dio.put<Map<String, dynamic>>('$_base/awards/${award.id}', data: {
          'competitionAwardId': awardId,
          'submissionId': submissionId,
          'remarks': remarks,
          'rowVersion': award.rowVersion,
        });
        return AwardingDetail.fromJson(res.data!);
      });

  /// DELETE .../awards/{id}?rowVersion=...: xóa giải khi chưa công bố.
  Future<AwardingDetail> delete(IssuedAward award) => guardApi(() async {
        final res = await _dio.delete<Map<String, dynamic>>('$_base/awards/${award.id}',
            queryParameters: {'rowVersion': award.rowVersion});
        return AwardingDetail.fromJson(res.data!);
      });

  /// POST .../competitions/{id}/publish
  Future<AwardingDetail> publish(int competitionId, {required bool confirmEmptySlots}) => guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('$_base/competitions/$competitionId/publish',
            data: {'confirmEmptySlots': confirmEmptySlots});
        return AwardingDetail.fromJson(res.data!);
      });

  /// POST .../awards/{id}/revoke: thu hồi giải sau công bố, bắt buộc lý do.
  Future<AwardingDetail> revoke(IssuedAward award, String reason) => guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('$_base/awards/${award.id}/revoke',
            data: {'reason': reason, 'rowVersion': award.rowVersion});
        return AwardingDetail.fromJson(res.data!);
      });

  /// PUT .../awards/{id}/delivery: đánh dấu đã/chưa phát thưởng.
  Future<AwardingDetail> setDelivery(IssuedAward award, bool delivered) => guardApi(() async {
        final res = await _dio.put<Map<String, dynamic>>('$_base/awards/${award.id}/delivery',
            data: {'delivered': delivered, 'rowVersion': award.rowVersion});
        return AwardingDetail.fromJson(res.data!);
      });

  /// PUT .../submissions/{id}/adjust-evaluation: sửa đánh giá sau công bố, bắt buộc lý do.
  Future<AwardingDetail> adjustEvaluation(int submissionId, EvaluationInput input, String reason) =>
      guardApi(() async {
        final res = await _dio.put<Map<String, dynamic>>('$_base/submissions/$submissionId/adjust-evaluation',
            data: {...input.toJson(), 'reason': reason});
        return AwardingDetail.fromJson(res.data!);
      });

  /// GET /api/competitions/{id}/result-adjustment: ngày điều chỉnh kết quả (null nếu chưa điều chỉnh).
  Future<DateTime?> resultAdjustedAt(int competitionId) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/competitions/$competitionId/result-adjustment');
        return _dateOrNull(res.data!['adjustedAt']);
      });
}

// ---------------- Provider ----------------

final awardingRepositoryProvider = Provider<AwardingRepository>((ref) => AwardingRepository(ref.watch(dioProvider)));

/// Cuộc thi CLOSED/GRADED trong tab "Trao giải".
final awardingCompetitionsProvider = FutureProvider<List<AwardingCompetition>>(
  (ref) => ref.watch(awardingRepositoryProvider).competitions(),
);

/// Chi tiết trao giải + checklist của một cuộc thi.
final awardingDetailProvider = FutureProvider.family<AwardingDetail, int>(
  (ref, id) => ref.watch(awardingRepositoryProvider).detail(id),
);

/// Bài đủ điều kiện nhận giải của một cuộc thi (autoDispose: luôn lấy danh sách mới khi mở lại hộp chọn).
final awardCandidatesProvider = FutureProvider.autoDispose.family<List<AwardCandidate>, int>(
  (ref, id) => ref.watch(awardingRepositoryProvider).candidates(id),
);

/// Ngày điều chỉnh kết quả sau công bố, học viên dùng để hiện nhãn "Kết quả đã điều chỉnh" (BR-34).
final resultAdjustedAtProvider = FutureProvider.family<DateTime?, int>(
  (ref, competitionId) => ref.watch(awardingRepositoryProvider).resultAdjustedAt(competitionId),
);

/// Làm mới mọi dữ liệu phụ thuộc trao giải/điều chỉnh: danh sách, chi tiết, ứng viên, giải công khai,
/// kết quả phía học viên, danh sách chấm bài. Nhận container để dùng được cả khi widget đã bị dispose
/// (lấy bằng `ProviderScope.containerOf(context)`).
void refreshAwardingData(ProviderContainer c) {
  c.invalidate(awardingCompetitionsProvider);
  c.invalidate(awardingDetailProvider);
  c.invalidate(awardCandidatesProvider);
  c.invalidate(resultAdjustedAtProvider);
  c.invalidate(competitionAwardsProvider);
  c.invalidate(allMySubmissionsProvider);
  c.invalidate(competitionListProvider);
  c.invalidate(gradingCompetitionsProvider);
  c.invalidate(gradingListProvider);
  c.invalidate(gradingDetailProvider);
  c.invalidate(submissionDetailProvider);
}

/// Xóa dữ liệu trao giải đang giữ khi đổi tài khoản.
void resetAwardingData(Ref ref) {
  ref.invalidate(awardingCompetitionsProvider);
  ref.invalidate(awardingDetailProvider);
  ref.invalidate(awardCandidatesProvider);
  ref.invalidate(resultAdjustedAtProvider);
}
