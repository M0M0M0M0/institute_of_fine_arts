import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';

/// A closed competition that still has ungraded entries.
class ToGradeItem {
  const ToGradeItem({
    required this.competitionId,
    required this.title,
    required this.endDate,
    required this.total,
    required this.evaluated,
    this.hasCover = false,
  });

  final int competitionId;
  final String title;
  final DateTime? endDate;
  final int total;
  final int evaluated;
  final bool hasCover;

  int get remaining => (total - evaluated).clamp(0, total);

  factory ToGradeItem.fromJson(Map<String, dynamic> json) => ToGradeItem(
        competitionId: json['competitionId'] as int,
        title: (json['title'] as String?) ?? '',
        endDate: json['endDate'] == null ? null : DateTime.tryParse(json['endDate'] as String),
        total: (json['total'] as int?) ?? 0,
        evaluated: (json['evaluated'] as int?) ?? 0,
        hasCover: (json['hasCover'] as bool?) ?? false,
      );
}

/// A fully graded competition whose results are not published yet.
class ToPublishItem {
  const ToPublishItem({
    required this.competitionId,
    required this.title,
    required this.emptySlots,
    this.hasCover = false,
  });

  final int competitionId;
  final String title;

  /// Award slots still empty (publishing asks the staff to confirm leaving them empty).
  final int emptySlots;
  final bool hasCover;

  factory ToPublishItem.fromJson(Map<String, dynamic> json) => ToPublishItem(
        competitionId: json['competitionId'] as int,
        title: (json['title'] as String?) ?? '',
        emptySlots: (json['emptySlots'] as int?) ?? 0,
        hasCover: (json['hasCover'] as bool?) ?? false,
      );
}

/// `GET /api/staff/dashboard` – the staff to-do overview.
class StaffDashboard {
  const StaffDashboard({
    this.toGrade = const [],
    this.toPublish = const [],
    this.pendingProposals = 0,
    this.unpaidSales = 0,
  });

  final List<ToGradeItem> toGrade;
  final List<ToPublishItem> toPublish;

  /// Exhibition proposals waiting for review.
  final int pendingProposals;

  /// Sold works not yet paid out to the student.
  final int unpaidSales;

  bool get isEmpty => toGrade.isEmpty && toPublish.isEmpty && pendingProposals == 0 && unpaidSales == 0;

  factory StaffDashboard.fromJson(Map<String, dynamic> json) {
    List<T> list<T>(String key, T Function(Map<String, dynamic>) parse) =>
        ((json[key] as List?) ?? const []).map((e) => parse(e as Map<String, dynamic>)).toList();
    return StaffDashboard(
      toGrade: list('toGrade', ToGradeItem.fromJson),
      toPublish: list('toPublish', ToPublishItem.fromJson),
      pendingProposals: (json['pendingProposals'] as int?) ?? 0,
      unpaidSales: (json['unpaidSales'] as int?) ?? 0,
    );
  }
}

/// Staff home data. Refresh with `ref.invalidate(staffDashboardProvider)`.
final staffDashboardProvider = FutureProvider<StaffDashboard>((ref) {
  final dio = ref.watch(dioProvider);
  return guardApi(() async {
    final res = await dio.get<Map<String, dynamic>>('/api/staff/dashboard');
    return StaffDashboard.fromJson(res.data!);
  });
});
