import 'package:flutter/material.dart';

import '../app/tokens.dart';
import '../core/format.dart';
import '../features/competitions/competitions.dart';
import 'api_urls.dart';
import 'public_image.dart';
import 'status_chip.dart';

/// Competition card: cover image on top (stone placeholder when there is none),
/// status chip, title, dates, countdown ("5 days left") and entries count.
///
/// Give it a [width] for horizontal strips; leave it null to fill the available width.
class CompetitionCard extends StatelessWidget {
  const CompetitionCard({super.key, required this.competition, required this.onTap, this.width});

  final CompetitionSummary competition;
  final VoidCallback onTap;
  final double? width;

  @override
  Widget build(BuildContext context) {
    final c = competition;
    final text = Theme.of(context).textTheme;
    final countdown = c.countdownLabel;
    final showEntries = c.status == CompetitionStatus.ongoing || c.status == CompetitionStatus.closed;

    return SizedBox(
      width: width,
      child: Card(
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              AspectRatio(
                aspectRatio: 16 / 9,
                child: Stack(
                  fit: StackFit.expand,
                  children: [
                    if (c.hasCover)
                      PublicImage(url: ApiUrls.competitionCover(c.id), semanticLabel: 'Cover of ${c.title}')
                    else
                      const ImagePlaceholder(icon: Icons.palette_outlined),
                    Positioned(left: AppSpacing.sm, top: AppSpacing.sm, child: StatusChip(c.status)),
                  ],
                ),
              ),
              Padding(
                padding: const EdgeInsets.all(AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(c.title, style: text.titleMedium, maxLines: 2, overflow: TextOverflow.ellipsis),
                    if (c.topic != null && c.topic!.isNotEmpty) ...[
                      const SizedBox(height: 2),
                      Text(c.topic!, style: text.bodySmall, maxLines: 1, overflow: TextOverflow.ellipsis),
                    ],
                    const SizedBox(height: AppSpacing.sm),
                    Wrap(
                      spacing: AppSpacing.md,
                      runSpacing: AppSpacing.xs,
                      children: [
                        _Info(Icons.event_outlined, '${formatDate(c.startDate)} – ${formatDate(c.endDate)}'),
                        if (countdown != null)
                          _Info(Icons.hourglass_bottom, countdown, emphasis: c.status == CompetitionStatus.ongoing),
                        if (showEntries)
                          _Info(Icons.image_outlined, '${c.submissionCount} ${c.submissionCount == 1 ? 'entry' : 'entries'}'),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Info extends StatelessWidget {
  const _Info(this.icon, this.text, {this.emphasis = false});

  final IconData icon;
  final String text;
  final bool emphasis;

  @override
  Widget build(BuildContext context) {
    final color = emphasis ? AppColors.brassText : AppColors.muted;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 16, color: color),
        const SizedBox(width: 4),
        Flexible(
          child: Text(
            text,
            style: Theme.of(context)
                .textTheme
                .bodySmall!
                .copyWith(color: color, fontWeight: emphasis ? FontWeight.w600 : null),
          ),
        ),
      ],
    );
  }
}
