import 'package:flutter/material.dart';

import '../app/motion.dart';
import '../app/tokens.dart';
import '../features/competitions/competitions.dart';
import 'api_urls.dart';
import 'layout_widgets.dart';
import 'public_image.dart';
import 'status_chip.dart';

/// A competition for the staff lists (grading, awards): the cover with the status chip, the title, one line of
/// dates and a progress bar ("12 / 20 graded"). Same look as the public competition card, so the staff and the
/// public side feel like one place. With a mouse the cover draws in slowly.
class CompetitionProgressCard extends StatefulWidget {
  const CompetitionProgressCard({
    super.key,
    required this.competitionId,
    required this.title,
    required this.hasCover,
    required this.status,
    required this.dateLine,
    required this.progressLabel,
    required this.progressValue,
    required this.progressTotal,
    required this.progressColor,
    required this.onTap,
    this.footer,
  });

  final int competitionId;
  final String title;
  final bool hasCover;
  final CompetitionStatus status;

  /// E.g. "Closed 20 Nov 2026".
  final String dateLine;
  final String progressLabel;
  final int progressValue;
  final int progressTotal;
  final Color progressColor;
  final VoidCallback onTap;

  /// Optional extra line under the bar (e.g. "Results adjusted on …").
  final Widget? footer;

  @override
  State<CompetitionProgressCard> createState() => _CompetitionProgressCardState();
}

class _CompetitionProgressCardState extends State<CompetitionProgressCard> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final duration = motionDuration(context, AppMotion.med);
    return Pressable(
      onTap: widget.onTap,
      onHover: (hover) => setState(() => _hover = hover),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          AspectRatio(
            aspectRatio: 16 / 9,
            child: Stack(
              fit: StackFit.expand,
              children: [
                ClipRect(
                  child: AnimatedScale(
                    scale: _hover ? 1.04 : 1,
                    duration: duration,
                    curve: AppMotion.curve,
                    child: widget.hasCover
                        ? PublicImage(
                            url: ApiUrls.competitionCover(widget.competitionId),
                            semanticLabel: 'Cover of ${widget.title}',
                          )
                        : const ImagePlaceholder(icon: Icons.palette_outlined),
                  ),
                ),
                Positioned(left: AppSpacing.sm, top: AppSpacing.sm, child: StatusChip(widget.status)),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.md),
          AnimatedDefaultTextStyle(
            duration: duration,
            style: text.titleMedium!.copyWith(color: _hover ? AppColors.brassText : AppColors.graphite),
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            child: Text(widget.title),
          ),
          const SizedBox(height: 2),
          Text(widget.dateLine, style: text.bodySmall),
          const SizedBox(height: AppSpacing.md),
          ProgressBarRow(
            label: widget.progressLabel,
            value: widget.progressValue,
            total: widget.progressTotal,
            color: widget.progressColor,
          ),
          if (widget.footer != null) widget.footer!,
        ],
      ),
    );
  }
}
