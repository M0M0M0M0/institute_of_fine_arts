import 'package:dio/dio.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../competitions/competitions.dart';
import '../submissions/submissions.dart';

/// Route prefix of the grading screens: `/teacher` for a teacher (who grades), `/staff` for staff (published
/// results, read only). Taken from the current location, so the same screens serve both shells.
String gradingBase(BuildContext context) =>
    GoRouterState.of(context).matchedLocation.startsWith('/teacher') ? '/teacher' : '/staff';

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
    this.hasCover = false,
  });

  final bool hasCover;

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
        hasCover: (json['hasCover'] as bool?) ?? false,
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
    this.hasThumbnail = true,
    this.gradedByMe = false,
  });

  /// The signed-in judge has graded this entry (the result [rating] needs the whole jury).
  final bool gradedByMe;

  /// Whether a thumbnail exists; defaults to true when the API does not say (a submission always has an image).
  final bool hasThumbnail;
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
        hasThumbnail: (json['hasThumbnail'] as bool?) ?? true,
        gradedByMe: (json['gradedByMe'] as bool?) ?? false,
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

/// A grading criterion of the competition with its weight in percent (BR-26).
class GradingCriterion {
  const GradingCriterion({required this.id, required this.name, this.description, required this.weight});

  /// competition_criterion id (what a score refers to).
  final int id;
  final String name;
  final String? description;
  final double weight;

  factory GradingCriterion.fromJson(Map<String, dynamic> json) => GradingCriterion(
        id: json['id'] as int,
        name: json['name'] as String,
        description: json['description'] as String?,
        weight: (json['weight'] as num).toDouble(),
      );
}

/// One criterion score (0–100) with an optional comment.
class GradingScore {
  const GradingScore({required this.criterionId, required this.score, this.comment});

  final int criterionId;
  final double? score;
  final String? comment;

  factory GradingScore.fromJson(Map<String, dynamic> json) => GradingScore(
        criterionId: json['criterionId'] as int,
        score: (json['score'] as num?)?.toDouble(),
        comment: json['comment'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'criterionId': criterionId,
        'score': score,
        if (comment != null && comment!.trim().isNotEmpty) 'comment': comment!.trim(),
      };
}

/// A member of the jury and whether they graded this entry (never their scores before publishing).
class JuryMember {
  const JuryMember({required this.name, required this.isChair, required this.done, required this.isMe});

  final String name;
  final bool isChair;
  final bool done;
  final bool isMe;

  factory JuryMember.fromJson(Map<String, dynamic> json) => JuryMember(
        name: json['name'] as String,
        isChair: (json['isChair'] as bool?) ?? false,
        done: (json['done'] as bool?) ?? false,
        isMe: (json['isMe'] as bool?) ?? false,
      );
}

/// One judge's column of the score sheet.
class ScoreSheetJudge {
  const ScoreSheetJudge({
    required this.name,
    required this.isChair,
    required this.disqualified,
    required this.totalScore,
    required this.scores,
    this.positivePoints,
    this.negativePoints,
    this.improvementAreas,
  });

  final String name;
  final bool isChair;
  final bool disqualified;
  final double? totalScore;
  final List<GradingScore> scores;
  final String? positivePoints;
  final String? negativePoints;
  final String? improvementAreas;

  factory ScoreSheetJudge.fromJson(Map<String, dynamic> json) => ScoreSheetJudge(
        name: json['name'] as String,
        isChair: (json['isChair'] as bool?) ?? false,
        disqualified: (json['disqualified'] as bool?) ?? false,
        totalScore: (json['totalScore'] as num?)?.toDouble(),
        scores: ((json['scores'] as List?) ?? const []).map((e) => GradingScore.fromJson(e as Map<String, dynamic>)).toList(),
        positivePoints: json['positivePoints'] as String?,
        negativePoints: json['negativePoints'] as String?,
        improvementAreas: json['improvementAreas'] as String?,
      );
}

/// The jury's full score sheet of an entry (BR-24): every judge's scores, the averages, the total and the rating.
class ScoreSheet {
  const ScoreSheet({
    required this.criteria,
    required this.judges,
    required this.averages,
    required this.totalScore,
    required this.rating,
    this.legacy = false,
  });

  final List<GradingCriterion> criteria;
  final List<ScoreSheetJudge> judges;
  final List<GradingScore> averages;
  final double? totalScore;
  final SubmissionRating? rating;

  /// Graded before the jury existed: only the rating and the comments.
  final bool legacy;

  static ScoreSheet? fromJsonOrNull(Object? json) =>
      json == null ? null : ScoreSheet.fromJson(json as Map<String, dynamic>);

  factory ScoreSheet.fromJson(Map<String, dynamic> json) => ScoreSheet(
        criteria: ((json['criteria'] as List?) ?? const []).map((e) => GradingCriterion.fromJson(e as Map<String, dynamic>)).toList(),
        judges: ((json['judges'] as List?) ?? const []).map((e) => ScoreSheetJudge.fromJson(e as Map<String, dynamic>)).toList(),
        averages: ((json['averages'] as List?) ?? const []).map((e) => GradingScore.fromJson(e as Map<String, dynamic>)).toList(),
        totalScore: (json['totalScore'] as num?)?.toDouble(),
        rating: json['rating'] == null ? null : ratingFromJson(json['rating'] as String),
        legacy: (json['legacy'] as bool?) ?? false,
      );
}

/// The signed-in judge's grade; [rowVersion] is sent back to edit / delete it (conflict detection, BR-52).
class StaffEvaluation {
  const StaffEvaluation({
    required this.disqualified,
    required this.scores,
    required this.totalScore,
    required this.positivePoints,
    required this.negativePoints,
    required this.improvementAreas,
    required this.evaluatedByName,
    required this.evaluatedAt,
    required this.rowVersion,
  });

  final bool disqualified;
  final List<GradingScore> scores;
  final double? totalScore;
  final String? positivePoints;
  final String? negativePoints;
  final String? improvementAreas;
  final String evaluatedByName;
  final DateTime evaluatedAt;
  final String rowVersion;

  factory StaffEvaluation.fromJson(Map<String, dynamic> json) => StaffEvaluation(
        disqualified: (json['disqualified'] as bool?) ?? false,
        scores: ((json['scores'] as List?) ?? const []).map((e) => GradingScore.fromJson(e as Map<String, dynamic>)).toList(),
        totalScore: (json['totalScore'] as num?)?.toDouble(),
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
    this.criteria = const [],
    this.jury = const [],
    this.resultRating,
    this.resultScore,
    this.scoreSheet,
    this.imageIds = const [],
  });

  /// Criteria of the competition with their weights.
  final List<GradingCriterion> criteria;

  /// Who is on the jury and who already graded this entry.
  final List<JuryMember> jury;

  /// The entry's result once the whole jury graded it (average of the judges' totals and its rating).
  final SubmissionRating? resultRating;
  final double? resultScore;

  /// Every judge's scores (after publishing).
  final ScoreSheet? scoreSheet;

  /// Pictures of the entry (submission_image ids, cover first).
  final List<int> imageIds;

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
        criteria: ((json['criteria'] as List?) ?? const []).map((e) => GradingCriterion.fromJson(e as Map<String, dynamic>)).toList(),
        jury: ((json['jury'] as List?) ?? const []).map((e) => JuryMember.fromJson(e as Map<String, dynamic>)).toList(),
        resultRating: json['resultRating'] == null ? null : ratingFromJson(json['resultRating'] as String),
        resultScore: (json['resultScore'] as num?)?.toDouble(),
        scoreSheet: ScoreSheet.fromJsonOrNull(json['scoreSheet']),
        imageIds: ((json['imageIds'] as List?) ?? const []).map((e) => e as int).toList(),
      );
}

/// The judge's grade sent to the API; [rowVersion] is required when the judge already graded the entry.
class EvaluationInput {
  const EvaluationInput({
    this.disqualified = false,
    this.scores = const [],
    required this.positivePoints,
    required this.negativePoints,
    required this.improvementAreas,
    this.rowVersion,
  });

  final bool disqualified;
  final List<GradingScore> scores;
  final String positivePoints;
  final String negativePoints;
  final String improvementAreas;
  final String? rowVersion;

  Map<String, dynamic> toJson() => {
        'disqualified': disqualified,
        'scores': disqualified ? const [] : scores.map((s) => s.toJson()).toList(),
        'positivePoints': positivePoints,
        'negativePoints': negativePoints,
        'improvementAreas': improvementAreas,
        if (rowVersion != null) 'rowVersion': rowVersion,
      };
}

// ---------------- Hàm thuần ----------------

/// Parses a score typed by the judge: 0–100 with at most 2 decimals (comma or dot); null when invalid or empty.
double? parseScore(String text) {
  final t = text.trim().replaceAll(',', '.');
  if (!RegExp(r'^\d{1,3}(\.\d{1,2})?$').hasMatch(t)) return null;
  final v = double.parse(t);
  return v <= 100 ? v : null;
}

/// Weighted total of one judge (Σ score × weight / 100), null until every criterion has a valid score.
double? judgeTotal(List<GradingCriterion> criteria, Map<int, double?> scores) {
  if (criteria.isEmpty) return null;
  var total = 0.0;
  for (final c in criteria) {
    final s = scores[c.id];
    if (s == null) return null;
    total += s * c.weight / 100;
  }
  return total;
}

/// Client-side check of BR-23; the server still decides. A disqualification needs its reason (weaknesses field);
/// otherwise every criterion needs a score from 0 to 100 and the strengths are required.
String? validateEvaluation({
  required bool disqualified,
  required List<GradingCriterion> criteria,
  required Map<int, String> scoreTexts,
  required String positive,
  required String negative,
}) {
  if (disqualified) {
    return negative.trim().isEmpty ? 'A disqualification needs a reason' : null;
  }
  if (criteria.isEmpty) return 'This competition has no grading criteria yet';
  for (final c in criteria) {
    if (parseScore(scoreTexts[c.id] ?? '') == null) {
      return 'Give ${c.name} a score from 0 to 100 (at most 2 decimals)';
    }
  }
  return positive.trim().isEmpty ? 'Please enter the strengths' : null;
}

/// "86.40" – a score with 2 decimals.
String formatScore(double value) => value.toStringAsFixed(2);

/// Staff-facing rating name ("Best", "Better", ...), the same wording as the chips.
String ratingName(SubmissionRating rating) => switch (rating) {
      SubmissionRating.best => 'Best',
      SubmissionRating.better => 'Better',
      SubmissionRating.good => 'Good',
      SubmissionRating.moderate => 'Moderate',
      SubmissionRating.normal => 'Normal',
      SubmissionRating.disqualified => 'Disqualified',
    };

/// Staff-facing name of the text that goes with an artwork.
String textTypeName(TextType type) => switch (type) {
      TextType.poem => 'Poem',
      TextType.quotation => 'Quotation',
      TextType.story => 'Story',
      TextType.other => 'Other',
    };

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
