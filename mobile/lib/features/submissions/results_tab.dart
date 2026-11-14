import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/async_view.dart';
import '../../core/format.dart';
import '../awarding/awarding_widgets.dart';
import '../competitions/competition_widgets.dart';
import '../competitions/competitions.dart';
import 'submission_widgets.dart';
import 'submissions.dart';

/// Danh sách giải đã trao của một cuộc thi; dùng ở chi tiết cuộc thi và tab "Kết quả & giải" (F-M06-09).
class AwardResultsList extends ConsumerWidget {
  const AwardResultsList({super.key, required this.competitionId});

  final int competitionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final awards = ref.watch(competitionAwardsProvider(competitionId));
    // Nhãn "Kết quả đã điều chỉnh ngày …" (BR-34) chỉ hiện khi cuộc thi từng được điều chỉnh sau công bố
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [AdjustedBadge(competitionId: competitionId), _awardsView(context, awards)],
    );
  }

  Widget _awardsView(BuildContext context, AsyncValue<List<AwardResult>> awards) {
    return switch (awards) {
      AsyncData(:final value) => value.isEmpty
          // API chỉ trả giải khi đã công bố kết quả
          ? const Padding(padding: EdgeInsets.symmetric(vertical: 8), child: Text('Chưa công bố giải thưởng.'))
          : Column(
              children: [
                for (final a in value)
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: CircleAvatar(child: Text('${a.rank}')),
                    title: Text('${a.awardName} • ${formatVnd(a.prizeValue)}'),
                    subtitle: Text('${a.studentName} – ${a.paintingTitle}'),
                  ),
              ],
            ),
      AsyncError(:final error) => Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Text('Không tải được giải thưởng: $error'),
        ),
      _ => const Padding(padding: EdgeInsets.all(8), child: Center(child: CircularProgressIndicator())),
    };
  }
}

/// Tab "Kết quả & giải": bài của tôi đã có xếp loại + giải thưởng các cuộc thi đã công bố (F-M06-07, 09).
class ResultsTab extends ConsumerWidget {
  const ResultsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final mine = ref.watch(allMySubmissionsProvider);
    final competitions = ref.watch(competitionListProvider);

    // Làm mới cả hai nguồn dữ liệu
    Future<void> refresh() async {
      ref.invalidate(competitionListProvider);
      for (final c in competitions.value ?? const <CompetitionSummary>[]) {
        ref.invalidate(competitionAwardsProvider(c.id));
      }
      ref.invalidate(allMySubmissionsProvider);
      await ref.read(allMySubmissionsProvider.future);
    }

    return AsyncValueView(
      value: mine,
      onRetry: () => ref.invalidate(allMySubmissionsProvider),
      data: (items) {
        // Bài đã có xếp loại hoặc thuộc cuộc thi đã công bố
        final graded = items.where((s) => s.rating != null || s.competitionStatus == CompetitionStatus.graded).toList();
        final gradedCompetitions =
            (competitions.value ?? const <CompetitionSummary>[]).where((c) => c.status == CompetitionStatus.graded);

        return RefreshIndicator(
          onRefresh: refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(16),
            children: [
              const SectionTitle('Kết quả bài của tôi'),
              if (graded.isEmpty) const Text('Chưa có bài nào được chấm hoặc công bố kết quả.'),
              for (final s in graded)
                Card(
                  child: ListTile(
                    title: Text(s.title),
                    subtitle: Text('${s.competitionTitle}\nNộp: ${formatDateTime(s.submittedAt)}'),
                    isThreeLine: true,
                    trailing: s.rating == null ? null : RatingChip(s.rating!),
                    onTap: () => context.push('/student/submissions/${s.id}'),
                  ),
                ),
              const SectionTitle('Giải thưởng đã trao'),
              if (gradedCompetitions.isEmpty) const Text('Chưa có cuộc thi nào công bố kết quả.'),
              for (final c in gradedCompetitions) ...[
                Text(c.title, style: Theme.of(context).textTheme.titleMedium),
                AwardResultsList(competitionId: c.id),
                const SizedBox(height: 8),
              ],
            ],
          ),
        );
      },
    );
  }
}
