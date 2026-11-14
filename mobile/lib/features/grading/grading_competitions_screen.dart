import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/async_view.dart';
import '../../core/format.dart';
import '../competitions/competition_widgets.dart';
import '../competitions/competitions.dart';
import 'grading.dart';

/// Tab "Chấm bài" của giáo viên: chọn cuộc thi, xem tiến độ "đã chấm x/y" (UC-05 bước 1, F-M07-09).
class GradingCompetitionsTab extends ConsumerWidget {
  const GradingCompetitionsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final competitions = ref.watch(gradingCompetitionsProvider);
    return AsyncValueView(
      value: competitions,
      onRetry: () => ref.invalidate(gradingCompetitionsProvider),
      data: (items) => RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(gradingCompetitionsProvider);
          await ref.read(gradingCompetitionsProvider.future);
        },
        child: items.isEmpty
            ? ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                children: const [Padding(padding: EdgeInsets.all(32), child: Center(child: Text('Chưa có cuộc thi nào để chấm.')))],
              )
            : ListView.builder(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(16),
                itemCount: items.length,
                itemBuilder: (context, i) => _CompetitionTile(item: items[i]),
              ),
      ),
    );
  }
}

class _CompetitionTile extends StatelessWidget {
  const _CompetitionTile({required this.item});

  final GradingCompetition item;

  @override
  Widget build(BuildContext context) {
    final total = item.submissionCount;
    final ongoing = item.status == CompetitionStatus.ongoing;
    return Card(
      child: InkWell(
        onTap: () => context.push('/staff/competitions/${item.id}/submissions'),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(child: Text(item.title, style: Theme.of(context).textTheme.titleMedium)),
                  StatusChip(item.status),
                ],
              ),
              const SizedBox(height: 4),
              Text('Hạn nộp: ${formatDate(item.endDate)}'),
              const SizedBox(height: 8),
              // Thanh tiến độ chấm
              LinearProgressIndicator(value: total == 0 ? 0 : item.evaluatedCount / total),
              const SizedBox(height: 4),
              Text('Đã chấm ${progressLabel(item.evaluatedCount, total)} bài'),
              // UC-05 1a: chưa hết hạn nộp thì chưa được chấm
              if (ongoing)
                Text('Chưa hết hạn nộp – chưa thể chấm.',
                    style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
          ),
        ),
      ),
    );
  }
}
