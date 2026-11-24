import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/tokens.dart';
import '../../core/api_client.dart';

// ---------------- Model (khớp DTO của API) ----------------

/// Trạng thái cuộc thi do API tính (BR-07).
enum CompetitionStatus { upcoming, ongoing, closed, graded }

CompetitionStatus statusFromJson(String value) =>
    CompetitionStatus.values.firstWhere((s) => s.name.toLowerCase() == value.toLowerCase());

extension CompetitionStatusView on CompetitionStatus {
  String get label => switch (this) {
        CompetitionStatus.upcoming => 'Upcoming',
        CompetitionStatus.ongoing => 'Open',
        CompetitionStatus.closed => 'Closed',
        CompetitionStatus.graded => 'Results published',
      };

  /// Status colour of the web palette (the scheme argument is kept for old callers).
  Color color([ColorScheme? scheme]) => switch (this) {
        CompetitionStatus.upcoming => AppColors.statusUpcoming,
        CompetitionStatus.ongoing => AppColors.statusOngoing,
        CompetitionStatus.closed => AppColors.statusClosed,
        CompetitionStatus.graded => AppColors.statusPublished,
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
    this.hasCover = false,
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

  /// True when the competition has a cover image (`/files/competitions/{id}/cover`); false when the API does not say.
  final bool hasCover;

  /// Nhãn đếm ngược cho cuộc thi sắp diễn ra; null nếu không phải UPCOMING.
  /// UPCOMING tính theo ngày nên thường >= 1; nếu 0 (lệch múi giờ/dữ liệu) thì hiện "hôm nay".
  String? get startCountdownLabel {
    if (status != CompetitionStatus.upcoming) return null;
    return daysUntilStart <= 0
        ? 'Starts today'
        : 'Starts in $daysUntilStart ${daysUntilStart == 1 ? 'day' : 'days'}';
  }

  /// Short countdown text for cards: "5 days left" (open), "Starts in 3 days" (upcoming); null otherwise.
  String? get countdownLabel {
    if (status == CompetitionStatus.upcoming) return startCountdownLabel;
    if (status != CompetitionStatus.ongoing) return null;
    if (daysLeft <= 0) return 'Last day';
    return daysLeft == 1 ? '1 day left' : '$daysLeft days left';
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
        hasCover: (json['hasCover'] as bool?) ?? false,
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
    this.timeline = const [],
  });

  /// Timeline: submission period, results and the staff's milestones, in date order (empty from an older API).
  final List<TimelineItem> timeline;

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
        timeline: ((json['timeline'] as List?) ?? const [])
            .map((e) => TimelineItem.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Where a milestone stands compared with today.
enum TimelineState { past, current, upcoming }

/// One line of a competition's timeline. [isSystem]: a date the system works with (submissions open / close,
/// results published); otherwise a milestone written by the staff (information only).
class TimelineItem {
  const TimelineItem({
    required this.date,
    required this.title,
    this.content,
    this.isSystem = false,
    this.state = TimelineState.upcoming,
  });

  final DateTime date;
  final String title;
  final String? content;
  final bool isSystem;
  final TimelineState state;

  factory TimelineItem.fromJson(Map<String, dynamic> json) => TimelineItem(
        date: DateTime.parse(json['date'] as String),
        title: json['title'] as String,
        content: json['content'] as String?,
        isSystem: (json['isSystem'] as bool?) ?? false,
        state: switch ((json['state'] as String? ?? '').toLowerCase()) {
          'past' => TimelineState.past,
          'current' => TimelineState.current,
          _ => TimelineState.upcoming,
        },
      );
}

/// Học viên đạt giải hiển thị trên trang chủ.
class Winner {
  const Winner({
    required this.studentName,
    required this.awardName,
    required this.rank,
    required this.paintingTitle,
    this.submissionId,
    this.hasThumbnail = false,
  });

  /// Id of the winning submission (public image at `/files/submissions/{id}/thumb`); null when the API does not say.
  final int? submissionId;
  final bool hasThumbnail;

  final String studentName;
  final String awardName;
  final int rank;
  final String paintingTitle;

  factory Winner.fromJson(Map<String, dynamic> json) => Winner(
        studentName: json['studentName'] as String,
        awardName: json['awardName'] as String,
        rank: json['rank'] as int,
        paintingTitle: json['paintingTitle'] as String,
        submissionId: json['submissionId'] as int?,
        hasThumbnail: (json['hasThumbnail'] as bool?) ?? false,
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
