import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/format.dart';
import 'awarding.dart';

/// Nhãn "Kết quả đã điều chỉnh ngày …" (BR-34) hiện cho nhân viên và học viên.
class AdjustedLabel extends StatelessWidget {
  const AdjustedLabel(this.adjustedAt, {super.key});

  final DateTime adjustedAt;

  @override
  Widget build(BuildContext context) {
    final color = Theme.of(context).colorScheme.tertiary;
    return Container(
      margin: const EdgeInsets.symmetric(vertical: 4),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(8)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.edit_note, size: 18, color: color),
          const SizedBox(width: 6),
          Flexible(
            child: Text('Kết quả đã điều chỉnh ngày ${formatDate(adjustedAt)}',
                style: TextStyle(color: color, fontWeight: FontWeight.w600)),
          ),
        ],
      ),
    );
  }
}

/// Hỏi API xem cuộc thi đã công bố có được điều chỉnh không; có thì hiện nhãn, không thì không hiện gì.
/// Lỗi hoặc đang tải cũng không hiện (nhãn chỉ là thông tin phụ, không được làm hỏng màn kết quả).
class AdjustedBadge extends ConsumerWidget {
  const AdjustedBadge({super.key, required this.competitionId});

  final int competitionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final adjusted = ref.watch(resultAdjustedAtProvider(competitionId));
    return switch (adjusted) {
      AsyncData(:final value) when value != null => Align(alignment: Alignment.centerLeft, child: AdjustedLabel(value)),
      _ => const SizedBox.shrink(),
    };
  }
}
