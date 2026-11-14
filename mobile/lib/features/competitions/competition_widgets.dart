import 'package:flutter/material.dart';

import '../../core/format.dart';
import 'competitions.dart';

/// Nhãn trạng thái cuộc thi có màu.
class StatusChip extends StatelessWidget {
  const StatusChip(this.status, {super.key});

  final CompetitionStatus status;

  @override
  Widget build(BuildContext context) {
    final color = status.color(Theme.of(context).colorScheme);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Text(status.label, style: TextStyle(color: color, fontSize: 12, fontWeight: FontWeight.w600)),
    );
  }
}

/// Thẻ cuộc thi dùng ở trang chủ và tab "Cuộc thi" của học viên.
class CompetitionCard extends StatelessWidget {
  const CompetitionCard({super.key, required this.competition, required this.onTap});

  final CompetitionSummary competition;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = competition;
    final textTheme = Theme.of(context).textTheme;
    final ongoing = c.status == CompetitionStatus.ongoing;

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(child: Text(c.title, style: textTheme.titleMedium)),
                  StatusChip(c.status),
                ],
              ),
              if (c.topic != null) ...[
                const SizedBox(height: 4),
                Text(c.topic!, style: textTheme.bodyMedium),
              ],
              const SizedBox(height: 8),
              Wrap(
                spacing: 16,
                runSpacing: 4,
                children: [
                  _Info(Icons.event, '${formatDate(c.startDate)} – ${formatDate(c.endDate)}'),
                  if (ongoing) _Info(Icons.hourglass_bottom, 'Còn ${c.daysLeft} ngày'),
                  // Cuộc thi sắp diễn ra: hiện đếm ngược đến ngày bắt đầu
                  if (c.startCountdownLabel != null) _Info(Icons.schedule, c.startCountdownLabel!),
                  if (ongoing) _Info(Icons.image, '${c.submissionCount} bài dự thi'),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Info extends StatelessWidget {
  const _Info(this.icon, this.text);

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 16, color: Theme.of(context).colorScheme.onSurfaceVariant),
        const SizedBox(width: 4),
        Text(text, style: Theme.of(context).textTheme.bodySmall),
      ],
    );
  }
}

/// Tiêu đề một nhóm nội dung.
class SectionTitle extends StatelessWidget {
  const SectionTitle(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(4, 16, 4, 8),
      child: Text(text, style: Theme.of(context).textTheme.titleLarge),
    );
  }
}
