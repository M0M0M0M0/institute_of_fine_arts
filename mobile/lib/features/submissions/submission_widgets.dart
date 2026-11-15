import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/tokens.dart';
import '../../core/api_client.dart';
import '../../shared/shared.dart';
import '../competitions/competitions.dart';
import 'submissions.dart';

/// Rating pill (Best, Better, ... Disqualified) in the shared status-chip style.
class RatingChip extends StatelessWidget {
  const RatingChip(this.rating, {super.key});

  final SubmissionRating rating;

  @override
  Widget build(BuildContext context) => StatusChip.rating(rating);
}

/// Image of an entry: bytes loaded through Dio (with the JWT), shown with `Image.memory`.
/// Also used by the staff screens.
class SubmissionImage extends ConsumerWidget {
  const SubmissionImage({super.key, required this.submissionId, this.height});

  final int submissionId;
  final double? height;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final image = ref.watch(submissionImageProvider(submissionId));
    return SizedBox(
      height: height,
      child: switch (image) {
        AsyncData(:final value) => Image.memory(value, fit: BoxFit.contain, width: double.infinity),
        AsyncError(:final error) => Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.broken_image_outlined, size: 40),
                const SizedBox(height: 8),
                Text(ApiException.from(error).message, textAlign: TextAlign.center),
                TextButton(
                  onPressed: () => ref.invalidate(submissionImageProvider(submissionId)),
                  child: const Text('Try again'),
                ),
              ],
            ),
          ),
        _ => const Center(child: CircularProgressIndicator()),
      },
    );
  }
}

/// What the student sees about the state of an entry, in words:
/// "Submitted" while the competition is open, "Waiting for results" after it closes,
/// then the rating / award.
String entryStateLabel(SubmissionListItem item) {
  if (item.rating != null) return item.rating!.label;
  return switch (item.competitionStatus) {
    CompetitionStatus.ongoing || CompetitionStatus.upcoming => 'Submitted',
    CompetitionStatus.closed || CompetitionStatus.graded => 'Waiting for results',
  };
}

/// Award of an entry among the published awards of its competition (null when none).
/// Matches by submission id when the API gives it, otherwise by title.
AwardResult? awardOf(SubmissionListItem item, List<AwardResult> awards) {
  for (final a in awards) {
    if (a.submissionId != null ? a.submissionId == item.id : a.paintingTitle == item.title) return a;
  }
  return null;
}

/// Horizontal row: framed thumbnail on the left, wall label + status on the right.
/// Used on Home and on My entries.
class SubmissionRow extends StatelessWidget {
  const SubmissionRow({
    super.key,
    required this.item,
    required this.onTap,
    this.awardName,
    this.note,
    this.showState = true,
  });

  final SubmissionListItem item;
  final VoidCallback onTap;

  /// Award shown in brass; also gives the thumbnail a gilt frame.
  final String? awardName;

  /// Extra line under the competition (e.g. "Closes 20/12/2026").
  final String? note;

  /// Show the state chip (Submitted / Waiting for results / rating).
  final bool showState;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return InkWell(
      onTap: onTap,
      borderRadius: AppRadius.smAll,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              width: 88,
              child: ArtworkFrame(
                awarded: awardName != null,
                aspectRatio: 1,
                matWidth: 5,
                child: item.hasThumbnail
                    ? AuthImage(submissionId: item.id, semanticLabel: item.title)
                    : const ImagePlaceholder(),
              ),
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    item.title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: text.titleSmall!.copyWith(fontStyle: FontStyle.italic, fontWeight: FontWeight.w500),
                  ),
                  const SizedBox(height: 2),
                  Text(item.competitionTitle, maxLines: 2, overflow: TextOverflow.ellipsis, style: text.bodyMedium),
                  if (note != null) Text(note!, style: text.bodySmall),
                  if (awardName != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 2),
                      child: Row(
                        children: [
                          const Icon(Icons.emoji_events_outlined, size: 16, color: AppColors.brassText),
                          const SizedBox(width: 4),
                          Flexible(
                            child: Text(
                              awardName!,
                              overflow: TextOverflow.ellipsis,
                              style: text.labelMedium!.copyWith(color: AppColors.brassText, fontWeight: FontWeight.w600),
                            ),
                          ),
                        ],
                      ),
                    ),
                  if (showState) ...[
                    const SizedBox(height: AppSpacing.xs),
                    if (item.rating != null)
                      StatusChip.rating(item.rating!)
                    else
                      StatusChip.custom(
                        label: entryStateLabel(item),
                        color: item.competitionStatus == CompetitionStatus.ongoing
                            ? AppColors.statusOngoing
                            : AppColors.statusClosed,
                      ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
