import 'package:flutter/material.dart';

// StatusChip and CompetitionCard now live in shared/ (re-exported here so old imports keep working).
export '../../shared/competition_card.dart';
export '../../shared/status_chip.dart' show StatusChip;

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
