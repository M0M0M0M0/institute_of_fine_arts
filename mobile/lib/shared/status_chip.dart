import 'package:flutter/material.dart';

import '../app/tokens.dart';
import '../features/competitions/competitions.dart';
import '../features/exhibitions/exhibitions.dart';
import '../features/submissions/submissions.dart';

/// Small coloured status pill.
///
/// `StatusChip(competitionStatus)` for competitions; `StatusChip.exhibition`, `.entry`,
/// `.rating` for the other enums; `StatusChip.custom` for anything else.
class StatusChip extends StatelessWidget {
  /// Competition status: Upcoming (blue), Open (green), Closed (grey), Published (navy).
  StatusChip(CompetitionStatus status, {Key? key})
      : this.custom(key: key, label: competitionStatusLabel(status), color: competitionStatusColor(status));

  /// Exhibition status.
  StatusChip.exhibition(ExhibitionStatus status, {Key? key})
      : this.custom(
          key: key,
          label: switch (status) {
            ExhibitionStatus.upcoming => 'Upcoming',
            ExhibitionStatus.ongoing => 'On now',
            ExhibitionStatus.finished => 'Finished',
            ExhibitionStatus.cancelled => 'Cancelled',
          },
          color: switch (status) {
            ExhibitionStatus.upcoming => AppColors.statusUpcoming,
            ExhibitionStatus.ongoing => AppColors.statusOngoing,
            ExhibitionStatus.finished => AppColors.statusClosed,
            ExhibitionStatus.cancelled => AppColors.danger,
          },
        );

  /// Exhibition entry status; a sold work that was paid out shows "Sold · paid".
  StatusChip.entry(EntryStatus status, {Key? key, bool paid = false})
      : this.custom(
          key: key,
          label: switch (status) {
            EntryStatus.pending => 'Pending review',
            EntryStatus.accepted => 'Accepted',
            EntryStatus.rejected => 'Rejected',
            EntryStatus.sold => paid ? 'Sold · paid' : 'Sold',
            EntryStatus.unsold => 'Unsold',
            EntryStatus.withdrawn => 'Withdrawn',
          },
          color: switch (status) {
            EntryStatus.pending => AppColors.statusUpcoming,
            EntryStatus.accepted => AppColors.statusOngoing,
            EntryStatus.sold => AppColors.brassText,
            EntryStatus.rejected || EntryStatus.withdrawn => AppColors.danger,
            EntryStatus.unsold => AppColors.statusClosed,
          },
        );

  /// Entry rating (BEST … DISQUALIFIED).
  StatusChip.rating(SubmissionRating rating, {Key? key})
      : this.custom(
          key: key,
          label: switch (rating) {
            SubmissionRating.best => 'Best',
            SubmissionRating.better => 'Better',
            SubmissionRating.good => 'Good',
            SubmissionRating.moderate => 'Moderate',
            SubmissionRating.normal => 'Normal',
            SubmissionRating.disqualified => 'Disqualified',
          },
          color: switch (rating) {
            SubmissionRating.best => AppColors.brassText,
            SubmissionRating.better || SubmissionRating.good => AppColors.statusOngoing,
            SubmissionRating.moderate || SubmissionRating.normal => AppColors.statusClosed,
            SubmissionRating.disqualified => AppColors.danger,
          },
        );

  const StatusChip.custom({super.key, required this.label, required this.color, this.icon});

  final String label;
  final Color color;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.10),
        borderRadius: AppRadius.smAll,
        border: Border.all(color: color.withValues(alpha: 0.35)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[Icon(icon, size: 14, color: color), const SizedBox(width: 4)],
          Flexible(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.labelSmall!.copyWith(color: color),
            ),
          ),
        ],
      ),
    );
  }
}

/// English label of a competition status.
String competitionStatusLabel(CompetitionStatus status) => switch (status) {
      CompetitionStatus.upcoming => 'Upcoming',
      CompetitionStatus.ongoing => 'Open',
      CompetitionStatus.closed => 'Closed',
      CompetitionStatus.graded => 'Results published',
    };

/// Status colour of a competition status (web palette).
Color competitionStatusColor(CompetitionStatus status) => switch (status) {
      CompetitionStatus.upcoming => AppColors.statusUpcoming,
      CompetitionStatus.ongoing => AppColors.statusOngoing,
      CompetitionStatus.closed => AppColors.statusClosed,
      CompetitionStatus.graded => AppColors.statusPublished,
    };
