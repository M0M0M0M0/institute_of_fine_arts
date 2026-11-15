import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/tokens.dart';
import '../../core/format.dart';
import '../awarding/awarding_widgets.dart';
import 'submissions.dart';

/// Published awards of one competition. Used on the competition detail and on My entries
/// (the old "Results & awards" tab was merged into My entries).
class AwardResultsList extends ConsumerWidget {
  const AwardResultsList({super.key, required this.competitionId});

  final int competitionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final awards = ref.watch(competitionAwardsProvider(competitionId));
    // The "Results adjusted on ..." badge only shows when the competition was adjusted after publishing (BR-34)
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [AdjustedBadge(competitionId: competitionId), _awardsView(context, awards)],
    );
  }

  Widget _awardsView(BuildContext context, AsyncValue<List<AwardResult>> awards) {
    return switch (awards) {
      AsyncData(:final value) => value.isEmpty
          // The API only returns awards once the results are published
          ? const Padding(padding: EdgeInsets.symmetric(vertical: 8), child: Text('No awards published yet.'))
          : Column(
              children: [
                for (final a in value)
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: CircleAvatar(
                      backgroundColor: AppColors.stone,
                      foregroundColor: AppColors.brassText,
                      child: Text('${a.rank}'),
                    ),
                    title: Text('${a.awardName} • ${formatMoney(a.prizeValue)}'),
                    subtitle: Text('${a.studentName} – ${a.paintingTitle}'),
                  ),
              ],
            ),
      AsyncError() => const Padding(
          padding: EdgeInsets.symmetric(vertical: 8),
          child: Text('Could not load the awards.'),
        ),
      _ => const Padding(padding: EdgeInsets.all(8), child: Center(child: CircularProgressIndicator())),
    };
  }
}
