import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/async_view.dart';
import '../../core/format.dart';
import '../competitions/competition_widgets.dart';
import '../competitions/competitions.dart';
import 'awarding.dart';
import 'awarding_widgets.dart';

/// Tab "Trao giải" của giáo viên: các cuộc thi đã hết hạn nộp hoặc đã công bố (UC-06 bước 1).
class AwardingCompetitionsTab extends ConsumerWidget {
  const AwardingCompetitionsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final competitions = ref.watch(awardingCompetitionsProvider);
    return AsyncValueView(
      value: competitions,
      onRetry: () => ref.invalidate(awardingCompetitionsProvider),
      data: (items) => RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(awardingCompetitionsProvider);
          await ref.read(awardingCompetitionsProvider.future);
        },
        child: items.isEmpty
            ? ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                children: const [
                  Padding(padding: EdgeInsets.all(32), child: Center(child: Text('Chưa có cuộc thi nào hết hạn nộp để trao giải.'))),
                ],
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

  final AwardingCompetition item;

  @override
  Widget build(BuildContext context) {
    final graded = item.status == CompetitionStatus.graded;
    final pending = item.submissionCount - item.evaluatedCount;
    return Card(
      child: InkWell(
        onTap: () => context.push('/staff/awarding/${item.id}'),
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
              const SizedBox(height: 4),
              Text('Đã trao ${slotsLabel(item.issuedSlots, item.totalSlots)} suất giải'),
              // Chưa công bố: nhắc còn bao nhiêu bài chưa chấm (BR-08)
              if (!graded && pending > 0)
                Text('Còn $pending bài chưa chấm – chưa thể công bố.',
                    style: TextStyle(color: Theme.of(context).colorScheme.error)),
              if (item.adjustedAt != null) AdjustedLabel(item.adjustedAt!),
            ],
          ),
        ),
      ),
    );
  }
}
