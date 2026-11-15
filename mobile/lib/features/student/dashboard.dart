import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../competitions/competitions.dart';
import '../submissions/submissions.dart';

/// `GET /api/student/dashboard` – what the student should do next.
class StudentDashboard {
  const StudentDashboard({
    this.openToEnter = const [],
    this.editable = const [],
    this.newResults = const [],
    this.pendingProposals = 0,
    this.unpaidSales = 0,
  });

  /// Open competitions the student has not entered yet.
  final List<CompetitionSummary> openToEnter;

  /// Submitted entries that can still be edited (before the deadline).
  final List<SubmissionListItem> editable;

  /// Entries with a newly published rating / award.
  final List<SubmissionListItem> newResults;

  /// Exhibition proposals waiting for staff review.
  final int pendingProposals;

  /// Sold works whose money the student has not received.
  final int unpaidSales;

  /// True when there is nothing to show (all lists empty and both counters 0).
  bool get isEmpty =>
      openToEnter.isEmpty && editable.isEmpty && newResults.isEmpty && pendingProposals == 0 && unpaidSales == 0;

  factory StudentDashboard.fromJson(Map<String, dynamic> json) {
    List<T> list<T>(String key, T Function(Map<String, dynamic>) parse) =>
        ((json[key] as List?) ?? const []).map((e) => parse(e as Map<String, dynamic>)).toList();
    return StudentDashboard(
      openToEnter: list('openToEnter', CompetitionSummary.fromJson),
      editable: list('editable', SubmissionListItem.fromJson),
      newResults: list('newResults', SubmissionListItem.fromJson),
      pendingProposals: (json['pendingProposals'] as int?) ?? 0,
      unpaidSales: (json['unpaidSales'] as int?) ?? 0,
    );
  }
}

/// Student home data. Refresh with `ref.invalidate(studentDashboardProvider)`.
final studentDashboardProvider = FutureProvider<StudentDashboard>((ref) {
  final dio = ref.watch(dioProvider);
  return guardApi(() async {
    final res = await dio.get<Map<String, dynamic>>('/api/student/dashboard');
    return StudentDashboard.fromJson(res.data!);
  });
});
