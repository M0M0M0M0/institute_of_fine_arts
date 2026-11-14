import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';

// ---------------- Model (khớp DTO của API) ----------------

/// Trạng thái cuộc thi do API tính (BR-07).
enum CompetitionStatus { upcoming, ongoing, closed, graded }

CompetitionStatus statusFromJson(String value) =>
    CompetitionStatus.values.firstWhere((s) => s.name.toLowerCase() == value.toLowerCase());

extension CompetitionStatusView on CompetitionStatus {
  String get label => switch (this) {
        CompetitionStatus.upcoming => 'Sắp diễn ra',
        CompetitionStatus.ongoing => 'Đang diễn ra',
        CompetitionStatus.closed => 'Đang chấm',
        CompetitionStatus.graded => 'Đã công bố',
      };

  Color color(ColorScheme scheme) => switch (this) {
        CompetitionStatus.upcoming => scheme.tertiary,
        CompetitionStatus.ongoing => scheme.primary,
        CompetitionStatus.closed => scheme.secondary,
        CompetitionStatus.graded => scheme.outline,
      };
}

/// Một dòng cuộc thi trong danh sách.
class CompetitionSummary {
  const CompetitionSummary({
    required this.id,
    required this.title,
    required this.topic,
    required this.startDate,
    required this.endDate,
    required this.status,
    required this.daysLeft,
    this.daysUntilStart = 0,
    required this.submissionCount,
  });

  final int id;
  final String title;
  final String? topic;
  final DateTime startDate;
  final DateTime endDate;
  final CompetitionStatus status;
  final int daysLeft;

  /// Số ngày còn lại đến ngày bắt đầu (chỉ có nghĩa khi UPCOMING); API cũ không trả thì mặc định 0.
  final int daysUntilStart;
  final int submissionCount;

  /// Nhãn đếm ngược cho cuộc thi sắp diễn ra; null nếu không phải UPCOMING.
  /// UPCOMING tính theo ngày nên thường >= 1; nếu 0 (lệch múi giờ/dữ liệu) thì hiện "hôm nay".
  String? get startCountdownLabel {
    if (status != CompetitionStatus.upcoming) return null;
    return daysUntilStart <= 0 ? 'Bắt đầu hôm nay' : 'Bắt đầu sau $daysUntilStart ngày';
  }

  factory CompetitionSummary.fromJson(Map<String, dynamic> json) => CompetitionSummary(
        id: json['id'] as int,
        title: json['title'] as String,
        topic: json['topic'] as String?,
        startDate: DateTime.parse(json['startDate'] as String),
        endDate: DateTime.parse(json['endDate'] as String),
        status: statusFromJson(json['status'] as String),
        daysLeft: json['daysLeft'] as int,
        // Trường mới: thiếu thì coi như 0 để app không vỡ khi gặp API cũ
        daysUntilStart: (json['daysUntilStart'] as int?) ?? 0,
        submissionCount: json['submissionCount'] as int,
      );
}

/// Giải thưởng định nghĩa của cuộc thi.
class CompetitionAward {
  const CompetitionAward({
    required this.name,
    required this.rank,
    required this.prizeValue,
    required this.quantity,
    required this.description,
  });

  final String name;
  final int rank;
  final num prizeValue;
  final int quantity;
  final String? description;

  factory CompetitionAward.fromJson(Map<String, dynamic> json) => CompetitionAward(
        name: json['name'] as String,
        rank: json['rank'] as int,
        prizeValue: json['prizeValue'] as num,
        quantity: json['quantity'] as int,
        description: json['description'] as String?,
      );
}

/// Chi tiết cuộc thi.
class CompetitionDetail {
  const CompetitionDetail({
    required this.summary,
    required this.description,
    required this.conditions,
    required this.deadline,
    required this.awards,
  });

  final CompetitionSummary summary;
  final String? description;
  final String? conditions;
  final DateTime deadline;
  final List<CompetitionAward> awards;

  factory CompetitionDetail.fromJson(Map<String, dynamic> json) => CompetitionDetail(
        summary: CompetitionSummary.fromJson(json),
        description: json['description'] as String?,
        conditions: json['conditions'] as String?,
        deadline: DateTime.parse(json['deadline'] as String),
        awards: (json['awards'] as List)
            .map((e) => CompetitionAward.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Học viên đạt giải hiển thị trên trang chủ.
class Winner {
  const Winner({required this.studentName, required this.awardName, required this.rank, required this.paintingTitle});

  final String studentName;
  final String awardName;
  final int rank;
  final String paintingTitle;

  factory Winner.fromJson(Map<String, dynamic> json) => Winner(
        studentName: json['studentName'] as String,
        awardName: json['awardName'] as String,
        rank: json['rank'] as int,
        paintingTitle: json['paintingTitle'] as String,
      );
}

/// Dữ liệu trang chủ (F-M02-01..04).
class HomeData {
  const HomeData({
    required this.upcoming,
    required this.ongoing,
    required this.latestGradedCompetition,
    required this.latestWinners,
  });

  final List<CompetitionSummary> upcoming;
  final List<CompetitionSummary> ongoing;
  final String? latestGradedCompetition;
  final List<Winner> latestWinners;

  factory HomeData.fromJson(Map<String, dynamic> json) {
    List<CompetitionSummary> list(String key) => (json[key] as List)
        .map((e) => CompetitionSummary.fromJson(e as Map<String, dynamic>))
        .toList();
    return HomeData(
      upcoming: list('upcoming'),
      ongoing: list('ongoing'),
      latestGradedCompetition: json['latestGradedCompetition'] as String?,
      latestWinners: (json['latestWinners'] as List)
          .map((e) => Winner.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

// ---------------- Provider: gọi API ----------------

/// GET /api/home
final homeProvider = FutureProvider<HomeData>((ref) {
  final dio = ref.watch(dioProvider);
  return guardApi(() async {
    final res = await dio.get<Map<String, dynamic>>('/api/home');
    return HomeData.fromJson(res.data!);
  });
});

/// GET /api/competitions/{id}
final competitionDetailProvider = FutureProvider.family<CompetitionDetail, int>((ref, id) {
  final dio = ref.watch(dioProvider);
  return guardApi(() async {
    final res = await dio.get<Map<String, dynamic>>('/api/competitions/$id');
    return CompetitionDetail.fromJson(res.data!);
  });
});
