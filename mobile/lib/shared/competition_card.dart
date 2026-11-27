import 'package:flutter/material.dart';

import '../app/motion.dart';
import '../app/tokens.dart';
import '../core/format.dart';
import '../features/competitions/competitions.dart';
import 'api_urls.dart';
import 'public_image.dart';
import 'status_chip.dart';

/// Competition card, like a poster on the wall with its label under it: cover image (stone placeholder when there
/// is none) with the status chip, then title, topic and one quiet line of facts (dates · time left · entries).
/// With a mouse the picture draws in slowly and the title turns brass.
///
/// Give it a [width] for horizontal strips; leave it null to fill the available width.
class CompetitionCard extends StatefulWidget {
  const CompetitionCard({super.key, required this.competition, required this.onTap, this.width});

  final CompetitionSummary competition;
  final VoidCallback onTap;
  final double? width;

  @override
  State<CompetitionCard> createState() => _CompetitionCardState();
}

class _CompetitionCardState extends State<CompetitionCard> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = widget.competition;
    final text = Theme.of(context).textTheme;
    final countdown = c.countdownLabel;
    final showEntries = c.status == CompetitionStatus.ongoing || c.status == CompetitionStatus.closed;
    final duration = motionDuration(context, AppMotion.med);

    // Facts as one quiet line, separated by dots: dates · time left · entries
    final facts = [
      '${formatDate(c.startDate)} – ${formatDate(c.endDate)}',
      ?countdown,
      if (showEntries) '${c.submissionCount} ${c.submissionCount == 1 ? 'entry' : 'entries'}',
    ];

    return SizedBox(
      width: widget.width,
      child: Pressable(
        onTap: widget.onTap,
        onHover: (hover) => setState(() => _hover = hover),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            AspectRatio(
              aspectRatio: 4 / 3,
              child: Stack(
                fit: StackFit.expand,
                children: [
                  ClipRect(
                    child: AnimatedScale(
                      scale: _hover ? 1.04 : 1,
                      duration: duration,
                      curve: AppMotion.curve,
                      child: c.hasCover
                          ? PublicImage(url: ApiUrls.competitionCover(c.id), semanticLabel: 'Cover of ${c.title}')
                          : const ImagePlaceholder(icon: Icons.palette_outlined),
                    ),
                  ),
                  Positioned(left: AppSpacing.sm, top: AppSpacing.sm, child: StatusChip(c.status)),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            AnimatedDefaultTextStyle(
              duration: duration,
              style: text.titleMedium!.copyWith(color: _hover ? AppColors.brassText : AppColors.graphite),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              child: Text(c.title),
            ),
            if (c.topic != null && c.topic!.isNotEmpty) ...[
              const SizedBox(height: 2),
              Text(
                c.topic!,
                style: text.bodySmall!.copyWith(fontStyle: FontStyle.italic),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ],
            const SizedBox(height: AppSpacing.xs),
            // Each fact is kept whole: when the line is too long it breaks between facts, never inside one
            Wrap(
              spacing: AppSpacing.sm,
              children: [
                for (var i = 0; i < facts.length; i++)
                  Text(
                    i < facts.length - 1 ? '${facts[i]}  ·' : facts[i],
                    style: text.bodySmall!.copyWith(
                      color: c.status == CompetitionStatus.ongoing ? AppColors.brassText : AppColors.muted,
                    ),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
